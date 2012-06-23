﻿using System;
using System.Reflection;
using System.Threading;
using System.Web.Configuration;
using System.Web.Mvc;
using RebelCms.Cms.Web.Context;
using RebelCms.Framework;
using System.Linq;

namespace RebelCms.Cms.Web.Mvc.ActionInvokers
{
    /// <summary>
    /// Ensures that any filters of type 
    /// - IRequiresRoutableRequestContext or
    /// - IRequiresBackOfficeRequest Context 
    /// have their parameters setup propery
    /// </summary>
    public class RebelCmsBackOfficeActionInvoker : ControllerExtenderActionInvoker
    {
        protected IBackOfficeRequestContext BackOfficeRequestContext { get; private set; }

        public RebelCmsBackOfficeActionInvoker(IBackOfficeRequestContext backOfficeRequestContext)
        {
            BackOfficeRequestContext = backOfficeRequestContext;
        }

        protected override FilterInfo GetFilters(ControllerContext controllerContext, ActionDescriptor actionDescriptor)
        {
            var filters = base.GetFilters(controllerContext, actionDescriptor);
            foreach (var filter in filters.AuthorizationFilters.Cast<object>()
                .Concat(filters.ActionFilters.Cast<object>())
                .Concat(filters.ExceptionFilters.Cast<object>())
                .Concat(filters.ResultFilters.Cast<object>()))
            {
                var filterType = filter.GetType();
                if (typeof(IRequiresBackOfficeRequestContext).IsAssignableFrom(filterType))
                {
                    ((IRequiresBackOfficeRequestContext)filter).BackOfficeRequestContext = BackOfficeRequestContext;
                }
                else if (typeof(IRequiresRoutableRequestContext).IsAssignableFrom(filterType))
                {
                    ((IRequiresRoutableRequestContext)filter).RoutableRequestContext = BackOfficeRequestContext;
                }
            }
            return filters;
        }
    }
}