using System;
using System.Collections.Generic;
using System.Linq;
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
using Sitecore.Reflection;
using Practice.CustomDataProvider.Models;
using Sitecore.Globalization;

namespace Practice.CustomDataProvider.DataProviders
{
    public class SimpleHierachicalReadOnlyDataProvider : DataProvider
    {
        private Database ContentDB
        {
            get
            {
                return Factory.GetDatabase(_targetDatabaseName);
            }
        }

        private readonly string _targetDatabaseName;
        private readonly string _idTablePrefix;
        private readonly ID _simpleReadOnlyDataTemplateID;
        private readonly ID _rootItemID;
        private readonly ID _rootTemplateID;
        private readonly ProductRepository _productDataRepository;

        public SimpleHierachicalReadOnlyDataProvider(string targetDatabaseName, string rootTemplateId, string rootItemId, string simpleReadOnlyDataTemplateId, string idTablePrefix)
        {
            Assert.ArgumentNotNullOrEmpty(targetDatabaseName, "targetDatabaseName");
            Assert.ArgumentNotNullOrEmpty(rootTemplateId, "rootTemplateId");
            Assert.ArgumentNotNullOrEmpty(rootItemId, "rootItemId");
            Assert.ArgumentNotNullOrEmpty(simpleReadOnlyDataTemplateId, "simpleReadOnlyDataTemplateId");
            Assert.ArgumentNotNullOrEmpty(idTablePrefix, "idTablePrefix");

            _targetDatabaseName = targetDatabaseName;
            _idTablePrefix = idTablePrefix;

            if (!ID.TryParse(rootTemplateId, out _rootTemplateID))
                throw new InvalidOperationException(string.Format("Could not parse to ID using {0}", rootTemplateId));

            if (!ID.TryParse(rootItemId, out _rootItemID))
                throw new InvalidOperationException(string.Format("Could not parse to ID using {0}", rootItemId));

            if (!ID.TryParse(simpleReadOnlyDataTemplateId, out _simpleReadOnlyDataTemplateID))
                throw new InvalidOperationException(string.Format("Could not parse to ID using {0}", simpleReadOnlyDataTemplateId));

            // Get an in memory repository with some sample data
            _productDataRepository = new ProductRepository();
        }

        public override IDList GetChildIDs(ItemDefinition parentItem, CallContext context)
        {
            var parentItemID = parentItem.ID;
            var parentItemName = parentItem.Name;
            var parentItemTemplateID = parentItem.TemplateID;
            if (CanProcessParent(parentItem.ID))
            {
                // Do not need to drop to any other data providers so abort the context
                context.Abort();

                IEnumerable<Product> productCollection = _productDataRepository.GetHierarchicalDataCollection();

                // Get the key for this parent item
                var idTableEntries = IDTable.GetKeys(_idTablePrefix, parentItem.ID);

                IEnumerable<Product> filteredProductDataCollection = null;

                if (parentItem.ID == _rootItemID)
                {
                    filteredProductDataCollection = productCollection.Where(o => o.ParentId == null);
                }
                else if (idTableEntries.Any())
                {
                    var parentKey = idTableEntries.FirstOrDefault();

                    filteredProductDataCollection = productCollection.Where(o => o.ParentId == parentKey.Key);
                }

                // List of child item ids
                var itemIdList = new IDList();

                foreach (var productData in filteredProductDataCollection)
                {
                    var productId = productData.ProductId;

                    IDTableEntry mappedID = IDTable.GetID(_idTablePrefix, productId);

                    if (mappedID == null)
                    {
                        mappedID = IDTable.GetNewID(_idTablePrefix, productId, parentItem.ID);
                    }

                    itemIdList.Add(mappedID.ID);
                }

                // Are you sure you want to do this !
                context.DataManager.Database.Caches.DataCache.Clear();

                return itemIdList;
            }

            return base.GetChildIDs(parentItem, context);
        }

        private bool CanProcessParent(ID id)
        {
            var item = Factory.GetDatabase(_targetDatabaseName).Items[id];

            bool canProcess = false;

            var validParentItemTemplateIDs = new ID[] { _rootTemplateID, _simpleReadOnlyDataTemplateID };

            // Process when 1 - the item is the data provider root folder
            //              2 - the item is based on the template if one of the allowed templates
            // item.Paths.IsContentItem && 
            if (validParentItemTemplateIDs.Contains(item.TemplateID))
            {
                canProcess = true;
            }

            return canProcess;
        }
        public override ID GetRootID(CallContext context)
        {
            return _rootItemID;
        }
        public override ItemDefinition GetItemDefinition(ID itemID, CallContext context)
        {
            Assert.ArgumentNotNull(itemID, "itemID");

            if (context.CurrentResult == null)
            {
                var productKey = GetProductDataKeyFromIDTable(itemID);

                // If no idTable entries then this item is not mapped to any product data
                if (!string.IsNullOrEmpty(productKey))
                {
                    // Get the collection of product data
                    var productCollection = _productDataRepository.GetHierarchicalDataCollection();

                    // Get the product data that will appear as an item in the Sitecore content tree
                    var productData = productCollection.FirstOrDefault(o => o.ProductId == productKey);

                    if (productData != null)
                    {
                        var itemName = ItemUtil.ProposeValidItemName(productData.Name);

                        return new ItemDefinition(itemID, itemName, ID.Parse(_simpleReadOnlyDataTemplateID), ID.Null);
                    }
                }
            }

            return null;
        }

        private string GetProductDataKeyFromIDTable(ID itemID)
        {
            var idTableEntries = IDTable.GetKeys(_idTablePrefix, itemID);

            if (idTableEntries != null && idTableEntries.Length > 0)
                return idTableEntries[0].Key.ToString();

            return null;
        }

        public override ID GetParentID(ItemDefinition itemDefinition, CallContext context)
        {
            if (CanProcessItem(itemDefinition.ID))
            {
                context.Abort();

                var idTableEntries = IDTable.GetKeys(_idTablePrefix, itemDefinition.ID);

                if (idTableEntries.Any())
                {
                    return idTableEntries.First().ParentID;
                }
            }

            return base.GetParentID(itemDefinition, context);
        }

        private bool CanProcessItem(ID id)
        {
            return IDTable.GetKeys(_idTablePrefix, id).Length > 0;
        }

        public override FieldList GetItemFields(ItemDefinition item, VersionUri version, CallContext context)
        {
            var fields = new FieldList();

            if (CanProcessChild(item.ID))
            {
                if (context.DataManager.DataSource.ItemExists(item.ID))
                {
                   CacheManager.GetItemCache(context.DataManager.Database).RemoveItem(item.ID);
                }

                var template = TemplateManager.GetTemplate(_simpleReadOnlyDataTemplateID, ContentDB);
                if (template != null)
                {
                    var productKey = GetProductDataKeyFromIDTable(item.ID);

                    // If no idTable entries then this item is not mapped to any product data
                    if (!string.IsNullOrEmpty(productKey))
                    {
                        // Get the collection of product data
                        var productCollection = _productDataRepository.GetHierarchicalDataCollection();

                        // Get the product data that will appear as an item in the Sitecore content tree
                        var productData = productCollection.FirstOrDefault(o => o.ProductId == productKey);

                        if (productData != null)
                        {
                            foreach (var field in GetDataFields(template))
                            {
                                fields.Add(field.ID, GetFieldValue(field, productData));
                            }
                        }
                    }
                }
            }

            return fields;
        }

        private bool CanProcessChild(ID id)
        {
            if (IDTable.GetKeys(_idTablePrefix, id).Length > 0)
            {
                return true;
            }

            return false;
        }

        // Filters template fields to data fields only (excludes fields of a StandardTemplate data template).
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

            if (fieldValue == null)
            {
                fieldValue = string.Empty;
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

        
    }
}