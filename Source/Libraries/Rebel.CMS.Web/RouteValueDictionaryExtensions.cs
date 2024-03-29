﻿using System;
using System.Linq;
using System.Web.Mvc;
using System.Web.Routing;

namespace Rebel.Cms.Web
{
    public static class RouteValueDictionaryExtensions
    {

        /// <summary>
        /// Converts a route value dictionary to a form collection
        /// </summary>
        /// <param name="items"></param>
        /// <returns></returns>
        public static FormCollection ToFormCollection(this RouteValueDictionary items)
        {
            var formCollection = new FormCollection();
            foreach(var i in items )
            {
                formCollection.Add(i.Key, i.Value != null ? i.Value.ToString() : null);
            }
            return formCollection;
        }

        /// <summary>
        /// Returns the value of a mandatory item in the route items
        /// </summary>
        /// <param name="items"></param>
        /// <returns></returns>
        public static object GetRequiredObject(this RouteValueDictionary items, string key)
        {
            if (!items.Keys.Contains(key))
                throw new ArgumentNullException("The " + key + " query string parameter was not found but is required");
            return items[key];
        }

    }
}
