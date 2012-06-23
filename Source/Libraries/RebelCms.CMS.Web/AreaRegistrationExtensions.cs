﻿using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using System.Web.Routing;
using RebelCms.Cms.Web.Configuration;
using RebelCms.Cms.Web.Context;
using RebelCms.Cms.Web.Routing;
using RebelCms.Framework;

namespace RebelCms.Cms.Web
{
    internal static class AreaRegistrationExtensions
    {
        /// <summary>
        /// Creates a custom individual route for the specified controller plugin. Individual routes
        /// are required by controller plugins to map to a unique URL based on ID.
        /// </summary>
        /// <typeparam name="T">An PluginAttribute</typeparam>
        /// <param name="controllerName"></param>
        /// <param name="controllerType"></param>
        /// <param name="routes">An existing route collection</param>
        /// <param name="routeIdParameterName">the data token name for the controller plugin</param>
        /// <param name="controllerSuffixName">
        /// The suffix name that the controller name must end in before the "Controller" string for example:
        /// ContentTreeController has a controllerSuffixName of "Tree"
        /// </param>
        /// <param name="baseUrlPath">
        /// The base name of the URL to create for example: RebelCms/[PackageName]/Trees/ContentTree/1 has a baseUrlPath of "Trees"
        /// </param>
        /// <param name="defaultAction"></param>
        /// <param name="defaultId"></param>
        /// <param name="isDefaultRebelCmsPlugin">If it's a default (built in) RebelCms plugin, then make the route with the GUID</param>
        /// <param name="area"></param>
        /// <param name="controllerId"></param>
        /// <param name="appContext"></param>
        /// <param name="RebelCmsTokenValue">The DataToken value to set for the 'RebelCms' key, this defaults to 'backoffice' </param>
        internal static void RouteControllerPlugin(this AreaRegistration area, Guid controllerId, string controllerName, Type controllerType, RouteCollection routes,
                                                   string routeIdParameterName, string controllerSuffixName, string baseUrlPath, string defaultAction, object defaultId,
                                                   bool isDefaultRebelCmsPlugin, IRebelCmsApplicationContext appContext, string RebelCmsTokenValue = "backoffice")
        {
            Mandate.ParameterNotNullOrEmpty(controllerName, "controllerName");
            Mandate.ParameterNotNullOrEmpty(routeIdParameterName, "routeIdParameterName");
            Mandate.ParameterNotNullOrEmpty(controllerSuffixName, "controllerSuffixName");
            Mandate.ParameterNotNullOrEmpty(defaultAction, "defaultAction");
            Mandate.ParameterNotNull(controllerType, "controllerType");
            Mandate.ParameterNotNull(routes, "routes");
            Mandate.ParameterNotNull(defaultId, "defaultId");

            //the url structure will change whether or not it is a built in RebelCms plugin.
            //url to match - if it's a non-RebelCms plugin use the GUID to route the request.
            //routes are explicitly name with controller names.
            //if its a plugin controller, then the route will always start with /RebelCms/ (BackOfficePath)
            var url = isDefaultRebelCmsPlugin
                          ? area.AreaName + "/" + baseUrlPath + "/" + controllerName + "/{action}/{id}"
                          : baseUrlPath.IsNullOrWhiteSpace()
                                ? appContext.Settings.RebelCmsPaths.BackOfficePath + "/" + area.AreaName + "/" + controllerName + "/{action}/{id}"
                                : appContext.Settings.RebelCmsPaths.BackOfficePath + "/" + area.AreaName + "/" + baseUrlPath + "/" + controllerName + "/{action}/{id}";

            //create a new route with custom name, specified url, and the namespace of the controller plugin
            var controllerPluginRoute = routes.MapRoute(
                //name
                string.Format("RebelCms-{0}-{1}", controllerName, controllerId),
                //url format
                url,
                //set the namespace of the controller to match
                new[] { controllerType.Namespace });

            //set defaults
            controllerPluginRoute.Defaults = new RouteValueDictionary(
                new Dictionary<string, object>
                    {
                        { "controller", controllerName },
                        { routeIdParameterName, controllerId.ToString("N") },
                        { "action", defaultAction },
                        { "id", defaultId }    
                    });

            //constraints: only match controllers ending with 'controllerSuffixName' and only match this controller's ID for this route            
            controllerPluginRoute.Constraints = new RouteValueDictionary(
                new Dictionary<string, object>
                    {
                        { "controller", @"(\w+)" + controllerSuffixName },
                        { routeIdParameterName, Regex.Escape(controllerId.ToString("N")) }
                    });
            //now add our custom back office route constraint
            controllerPluginRoute.Constraints.Add("backOffice", new BackOfficeRouteConstraint(appContext));

            //match this area
            controllerPluginRoute.DataTokens.Add("area", area.AreaName);
            controllerPluginRoute.DataTokens.Add("RebelCms", RebelCmsTokenValue); //ensure the RebelCms token is set

        }
    }
}