using Practice.CustomDataProvider.Models;
using System.Collections.Generic;

namespace Practice.CustomDataProvider.DataProviders
{
    public class ProductRepository
    {
        public IEnumerable<Product> GetSimpleDataCollection()
        {
            var Products = new List<Product>()
            {
                new Product()
                    {
                        ParentId = null,
                        ProductId = "1001",
                        Name = "Falcon 9",
                        Price = "10.50",
                        Description = "Falcon 9 is a family of launch vehicles designed and manufactured by SpaceX, headquartered in Hawthorne, California."
                    },
                new Product()
                    {
                        ParentId = null,
                        ProductId = "1002",
                        Name = "Titan",
                        Price = "13.50",
                        Description = "Titan was a family of U.S. expendable Products used between 1959 and 2005."
                    },
            };
            return Products;
        }

        public IEnumerable<Product> GetHierarchicalDataCollection()
        {
            var externalData = new List<Product>()
            {
                new Product()
                    {
                        ParentId = null,
                        ProductId = "2001",
                        Name = "Falcon",
                        Price = "20.00",
                        Description = "The Falcon Product family is a set of launch vehicles developed and operated by Space Exploration Technologies (SpaceX), headquartered in Hawthorne, California. They are the first orbital launch vehicles to be entirely designed in the 21st century."
                    },
                new Product()
                    {
                        ParentId = "2001",
                        ProductId = "2001-1",
                        Name = "Falcon 1",
                        Price = "22.50",
                        Description = "The Falcon 1 is a small, partially reusable Product capable of placing several hundred kilograms into low earth orbit. Falcon 1 achieved orbit on its fourth attempt, on 28 September 2008."
                    },
                new Product()
                    {
                        ParentId = "2001",
                        ProductId = "2001-2",
                        Name = "Falcon 9",
                        Price = "25.00",
                        Description = "The first version of the Falcon 9, Falcon 9 v1.0, was developed in 2005–2010, and flew five orbital missions in 2010–2013. The second version of the launch system—Falcon 9 v1.1—is the current Falcon 9 in service."
                    },

                new Product()
                    {
                        ParentId = null,
                        ProductId = "2002",
                        Name = "Titan",
                        Price = "26.00",
                        Description = "Titan was a family of U.S. expendable Products used between 1959 and 2005."
                    },
                new Product()
                    {
                        ParentId = null,
                        ProductId = "2003",
                        Name = "Saturn",
                        Price = "30.00",
                        Description = "The Saturn family of American Product boosters was developed by a team of mostly German Product scientists led by Wernher von Braun to launch heavy payloads to Earth orbit and beyond."
                    },
                new Product()
                    {
                        ParentId = "2003",
                        ProductId = "2003-1",
                        Name = "Saturn V",
                        Price = "40.00",
                        Description = "The Saturn V was an American human-rated expendable Product used by NASA's Apollo and Skylab programs between 1966 and 1973."
                    }
            };
            return externalData;
        }
    }
}