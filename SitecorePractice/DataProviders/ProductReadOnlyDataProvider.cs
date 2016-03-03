using Sitecore.Caching;
using Sitecore.Collections;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.DataProviders;
using Sitecore.Data.IDTables;
using Sitecore.Data.Items;
using Sitecore.Data.Managers;
using Sitecore.Data.Templates;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Practice.CustomDataProvider.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Practice.CustomDataProvider.DataProviders
{
    public class ProductReadOnlyDataProvider : DataProvider
    {
        private readonly string _targetDatabaseName;
        private readonly string _idTablePrefix;
        private readonly ID _productTemplateID;
        private readonly ID _productRootTemplateID;

        private readonly IEnumerable<Product> _product;

        public ProductReadOnlyDataProvider(string targetDatabaseName, string productRootTemplateID, string productTemplateID, string idTablePrefix)
        {
            Assert.ArgumentNotNullOrEmpty(targetDatabaseName, "targetDatabaseName");
            Assert.ArgumentNotNullOrEmpty(productRootTemplateID, "rootTemplateId");
            Assert.ArgumentNotNullOrEmpty(productTemplateID, "simpleReadOnlyDataTemplateId");
            Assert.ArgumentNotNullOrEmpty(idTablePrefix, "idTablePrefix");

            _targetDatabaseName = targetDatabaseName;
            _idTablePrefix = idTablePrefix;

            if (!ID.TryParse(productRootTemplateID, out _productRootTemplateID))
                throw new InvalidOperationException(string.Format("Invalid product root template ID {0}", productRootTemplateID));

            if (!ID.TryParse(productTemplateID, out _productTemplateID))
                throw new InvalidOperationException(string.Format("Invalid product template ID {0}", productTemplateID));

            _product = new ProductRepository().GetSimpleDataCollection();
        }

        public override ItemDefinition GetItemDefinition(ID itemID, CallContext context)
        {
            Assert.IsNull(context.CurrentResult, "context.CurrentResult");

            Assert.ArgumentNotNull(itemID, "itemID");

            // Retrieve the product id from Sitecore IDTable
            var productId = GetProductDataKeyFromIDTable(itemID);

            if (!string.IsNullOrEmpty(productId))
            {
                // Retrieve the product data from the product collection
                var product = _product.FirstOrDefault(o => o.ProductId == productId);

                if (product != null)
                {
                    // Ensure the product item name is valid for the Sitecore content tree
                    var itemName = ItemUtil.ProposeValidItemName(product.Name);

                    // Return a Sitecore item definition for the product using the product template
                    return new ItemDefinition(itemID, itemName, ID.Parse(_productTemplateID), ID.Null);
                }
            }

            return null;
        }

        private string GetProductDataKeyFromIDTable(ID itemID)
        {
            var idTableEntries = IDTable.GetKeys(_idTablePrefix, itemID);

            if (idTableEntries.Any())
                return idTableEntries.First().Key;

            return null;
        }

        public override ID GetParentID(ItemDefinition itemDefinition, CallContext context)
        {
            var idTableEntries = IDTable.GetKeys(_idTablePrefix, itemDefinition.ID);

            if (idTableEntries.Any())
            {
                return idTableEntries.First().ParentID;
            }

            return base.GetParentID(itemDefinition, context);
        }

        public override IDList GetChildIDs(ItemDefinition parentItem, CallContext context)
        {
            if (CanProcessParent(parentItem.ID))
            {
                var itemIdList = new IDList();

                foreach (var product in _product)
                {
                    var productId = product.ProductId;

                    // Retrieve the Sitecore item ID mapped to this product
                    IDTableEntry mappedID = IDTable.GetID(_idTablePrefix, productId);

                    if (mappedID == null)
                    {
                        // Map this product to a Sitecore item ID
                        mappedID = IDTable.GetNewID(_idTablePrefix, productId, parentItem.ID);
                    }

                    itemIdList.Add(mappedID.ID);
                }

                context.DataManager.Database.Caches.DataCache.Clear();

                return itemIdList;
            }

            return base.GetChildIDs(parentItem, context);
        }

        public override LanguageCollection GetLanguages(CallContext context)
        {
            return null;
        }

        private bool CanProcessParent(ID id)
        {
            var item = Factory.GetDatabase(_targetDatabaseName).Items[id];

            bool canProcess = false;

            if (item.Paths.IsContentItem && item.TemplateID == _productRootTemplateID)
            {
                canProcess = true;
            }

            return canProcess;
        }

        public override FieldList GetItemFields(ItemDefinition itemDefinition, VersionUri version, CallContext context)
        {
            var fields = new FieldList();

            var idTableEntries = IDTable.GetKeys(_idTablePrefix, itemDefinition.ID);

            if (idTableEntries.Any())
            {
                if (context.DataManager.DataSource.ItemExists(itemDefinition.ID))
                {
                    CacheManager.GetItemCache(context.DataManager.Database).RemoveItem(itemDefinition.ID);
                }

                var template = TemplateManager.GetTemplate(_productTemplateID, Factory.GetDatabase(_targetDatabaseName));

                if (template != null)
                {
                    var productKey = GetProductDataKeyFromIDTable(itemDefinition.ID);

                    // If no idTable entries then this item is not mapped to any product data
                    if (!string.IsNullOrEmpty(productKey))
                    {
                        // Get the product data that will appear as an item in the Sitecore content tree
                        var product = _product.FirstOrDefault(o => o.ProductId == productKey);

                        if (product != null)
                        {
                            foreach (var field in GetDataFields(template))
                            {
                                fields.Add(field.ID, GetFieldValue(field, product));
                            }
                        }
                    }
                }
            }

            return fields;
        }

        protected virtual IEnumerable<TemplateField> GetDataFields(Template template)
        {
            return template.GetFields().Where(ItemUtil.IsDataField);
        }

        private string GetFieldValue(TemplateField field, Product product)
        {
            string fieldValue = string.Empty;

            switch (field.Name)
            {
                case "Name":
                    fieldValue = product.Name;
                    break;
                case "Price":
                    fieldValue = product.Price;
                    break;
                case "Description":
                    fieldValue = product.Description;
                    break;
                default:
                    break;
            }

            return fieldValue;
        }

        public override VersionUriList GetItemVersions(ItemDefinition item, CallContext context)
        {
            if (CanProcessItem(item.ID))
            {
                VersionUriList versions = new VersionUriList();

                // Just a little hack
                versions.Add(Language.Current, Sitecore.Data.Version.First);

                context.Abort();

                return versions;
            }

            return base.GetItemVersions(item, context);
        }

        private bool CanProcessItem(ID id)
        {
            return IDTable.GetKeys(_idTablePrefix, id).Length > 0;
        }
    }
}