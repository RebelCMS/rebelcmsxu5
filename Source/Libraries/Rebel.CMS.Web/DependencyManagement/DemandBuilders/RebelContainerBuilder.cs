﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using System.Web.Hosting;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security;
using Rebel.Cms.Web.Configuration;
using Rebel.Cms.Web.Context;
using Rebel.Cms.Web.IO;
using Rebel.Cms.Web.Mapping;
using Rebel.Cms.Web.Mvc.Areas;
using Rebel.Cms.Web.Mvc.ControllerFactories;
using Rebel.Cms.Web.Packaging;
using Rebel.Cms.Web.Routing;
using Rebel.Cms.Web.Security;
using Rebel.Cms.Web.System;
using Rebel.Cms.Web.System.Boot;

using Rebel.Framework;
using Rebel.Framework.Context;
using Rebel.Framework.DependencyManagement;
using System.Linq;

using Rebel.Framework.Localization.Configuration;
using Rebel.Framework.Localization.Web;
using Rebel.Framework.Persistence;
using Rebel.Framework.Persistence.Model.Constants;
using Rebel.Framework.Persistence.Model.Constants.Entities;
using Rebel.Framework.Security;
using Rebel.Framework.Security.Model.Entities;
using Rebel.Framework.Tasks;
using Rebel.Framework.TypeMapping;
using Rebel.Hive;
using FixedHiveIds = Rebel.Framework.Security.Model.FixedHiveIds;

namespace Rebel.Cms.Web.DependencyManagement.DemandBuilders
{
    /// <summary>
    /// A module that creates and build the IoC container that powers the Rebel core
    /// </summary>
    /// <typeparam name="TWebApp">The type of the web application defined in the web application's assembly</typeparam>
    public class RebelContainerBuilder<TWebApp> : IDependencyDemandBuilder
        where TWebApp : HttpApplication
    {
        #region Constructors

        /// <summary>
        /// Default constructor that uses the standard RebelComponentRegistrar as the IComponentRegistrar
        /// </summary>
        /// <param name="httpApp"></param>
        public RebelContainerBuilder(TWebApp httpApp)
        {
            _httpApp = httpApp;
            _settings = RebelSettings.GetSettings();
            _componentRegistrar = new RebelComponentRegistrar();
        }

        /// <summary>
        /// Custom constructor that can be used to assign a custom IComponentRegistrar
        /// </summary>
        /// <param name="httpApp"></param>
        /// <param name="componentRegistrar"></param>
        /// <param name="settings"></param>        
        public RebelContainerBuilder(TWebApp httpApp, IComponentRegistrar componentRegistrar, RebelSettings settings)
        {
            _httpApp = httpApp;
            _componentRegistrar = componentRegistrar;
            _settings = settings;
        }

        #endregion

        #region Private fields

        private readonly IComponentRegistrar _componentRegistrar;
        private readonly RebelSettings _settings;
        private readonly HttpApplication _httpApp;
        private readonly object _locker = new object();

        #endregion

        #region Events

        /// <summary>
        /// Event raised just before the container is built allow developers to register their own 
        /// objects in the container
        /// </summary>
        public event EventHandler<ContainerBuilderEventArgs> ContainerBuilding;

        /// <summary>
        /// Event raised after the container is built allow developers to register their own 
        /// objects in the container
        /// </summary>
        public event EventHandler<ContainerBuilderEventArgs> ContainerBuildingComplete;

        #endregion

        #region Protected event raising methods

        protected void OnContainerBuilding(ContainerBuilderEventArgs args)
        {
            if (ContainerBuilding != null)
            {
                ContainerBuilding(this, args);
            }
        }

        protected void OnContainerBuildingComplete(ContainerBuilderEventArgs args)
        {
            if (ContainerBuildingComplete != null)
            {
                ContainerBuildingComplete(this, args);
            }
        }

        #endregion

        #region IDependencyDemandBuilder Members

        /// <summary>Builds the dependency demands required by this implementation. </summary>
        /// <param name="builder">The <see cref="IContainerBuilder"/> .</param>
        /// <param name="builderContext"></param>
        public void Build(IContainerBuilder builder, IBuilderContext builderContext)
        {
            //raise the building event
            OnContainerBuilding(new ContainerBuilderEventArgs(builder));
            
            //register all of the abstract web types
            builder.AddDependencyDemandBuilder(new WebTypesDemandBuilder(_httpApp));
            
            var typeFinder = new TypeFinder();
            builder.ForInstanceOfType(typeFinder)
                .ScopedAs.Singleton();

            //register the rebel settings
            builder.ForFactory(x => RebelSettings.GetSettings())
                .KnownAsSelf()
                .ScopedAs.Singleton(); //only have one instance ever

            //register our MVC types
            builder.AddDependencyDemandBuilder(new MvcTypesDemandBuilder(typeFinder));

            // Register the IRoutableRequestContext
            builder.For<HttpRequestScopedCache>().KnownAs<AbstractScopedCache>().ScopedAs.Singleton();
            builder.For<HttpRuntimeApplicationCache>().KnownAs<AbstractApplicationCache>().ScopedAs.Singleton();
            builder.For<HttpRequestScopedFinalizer>().KnownAs<AbstractFinalizer>().ScopedAs.Singleton();
            //builder.For<DefaultFrameworkContext>().KnownAs<IFrameworkContext>().ScopedAs.Singleton();
            builder.For<RebelApplicationContext>().KnownAs<IRebelApplicationContext>().ScopedAs.Singleton();
            builder.For<RoutableRequestContext>().KnownAs<IRoutableRequestContext>().ScopedAs.HttpRequest();
            builder.For<DefaultBackOfficeRequestContext>().KnownAs<IBackOfficeRequestContext>().ScopedAs.HttpRequest();

            // TODO: Ensure this isn't created manually anywhere but tests, only via a factory: builder.ForType<IRebelRenderModel, RebelRenderContext>().Register().ScopedPerHttpRequest();
            builder.For<DefaultRenderModelFactory>().KnownAs<IRenderModelFactory>().
                ScopedAs.Singleton();

            // Register Hive provider
            //builder.AddDependencyDemandBuilder(new HiveDemandBuilder());
            builder.AddDependencyDemandBuilder(new Hive.DemandBuilders.HiveDemandBuilder());

            // Register Persistence provider loader
            //builder.AddDependencyDemandBuilder(new Framework.Persistence.DependencyManagement.DemandBuilders.LoadFromPersistenceConfig());
            builder.AddDependencyDemandBuilder(new Hive.DemandBuilders.LoadFromPersistenceConfig());

            // Register Cms bootstrapper
            // TODO: Split RebelContainerBuilder between Cms and Frontend variants / needs
            builder.For<CmsBootstrapper>().KnownAsSelf();
            // Register Frontend bootstrapper
            builder.ForFactory(
                x =>
                new RenderBootstrapper(
                    x.Resolve<IRebelApplicationContext>(),
                    x.Resolve<IRouteHandler>(RenderRouteHandler.SingletonServiceName),
                    x.Resolve<IRenderModelFactory>())
                )
                .KnownAsSelf();
            
            //register all component areas, loop through all found package folders
            //TODO: All other places querying for packages use the NuGet IO FileManager stuff, not the standard .Net IO classes
            var pluginFolder = new DirectoryInfo(_httpApp.Server.MapPath(_settings.PluginConfig.PluginsPath));
            foreach (var package in pluginFolder.GetDirectories(PluginManager.PackagesFolderName)
                .SelectMany(x => x.GetDirectories()
                    .Where(PluginManager.IsPackagePluginFolder)))
            {
                //register an area for this package
                builder.For<PackageAreaRegistration>()
                    .KnownAsSelf()
                    .WithNamedParam("packageFolder", package);
            }

            //register the RoutingEngine
            builder
                .For<DefaultRoutingEngine>()
                .KnownAs<IRoutingEngine>()
                .ScopedAs.HttpRequest();

            //register the package context
            builder
                .ForFactory(x => new DefaultPackageContext(x.Resolve<RebelSettings>(), HostingEnvironment.MapPath))
                //.For<DefaultPackageContext>()
                .KnownAs<IPackageContext>()
                .ScopedAs.Singleton();

            //register the PropertyEditorFactory
            builder.For<PropertyEditorFactory>()
                .KnownAs<IPropertyEditorFactory>()
                .ScopedAs.Singleton();

            //register the ParameterEditorFactory
            builder.For<ParameterEditorFactory>()
                .KnownAs<IParameterEditorFactory>()
                .ScopedAs.Singleton();

            //register the SecurityService
            builder.ForFactory(
                x =>
                new SecurityService(
                    x.Resolve<IMembershipService<User>>(),
                    x.Resolve<IMembershipService<Member>>(),
                    x.Resolve<IPermissionsService>(),
                    x.Resolve<IPublicAccessService>()))
                .KnownAs<ISecurityService>();

            //register the MembershipService
            builder.ForFactory(
                x =>
                    {
                        var frameworkContext = x.Resolve<IFrameworkContext>();
                        var hiveManager = x.Resolve<IHiveManager>();
                        MembershipProvider membershipProvider = Membership.Providers["UsersMembershipProvider"];
                        return new MembershipService<User, UserProfile>(
                             frameworkContext,
                             hiveManager,
                             "security://user-profiles",
                             "security://user-groups",
                    FixedHiveIds.UserProfileVirtualRoot,
                             membershipProvider,
                             _settings.MembershipProviders);
                    })
                .KnownAs<IMembershipService<User>>()
                .ScopedAs.Singleton();

            builder.ForFactory(
                x =>
                new MembershipService<Member, MemberProfile>(
                    x.Resolve<IFrameworkContext>(),
                    x.Resolve<IHiveManager>(),
                    "security://member-profiles",
                    "security://member-groups",
                    FixedHiveIds.MemberProfileVirtualRoot,
                    Membership.Providers["MembersMembershipProvider"],
                    _settings.MembershipProviders)
                )
                .KnownAs<IMembershipService<Member>>()
                .ScopedAs.Singleton();

            builder.ForFactory(
                x =>
                new PermissionsService(
                    x.Resolve<IHiveManager>(),
                    x.Resolve<IEnumerable<Lazy<Permission, PermissionMetadata>>>(),
                    x.Resolve<IMembershipService<User>>()))
                .KnownAs<IPermissionsService>()
                .ScopedAs.Singleton();

            builder.ForFactory(
                x =>
                new PublicAccessService(
                    x.Resolve<IHiveManager>(),
                    x.Resolve<IMembershipService<Member>>(),
                    x.Resolve<IFrameworkContext>()))
                .KnownAs<IPublicAccessService>()
                .ScopedAs.Singleton();

            //register the CmsAttributeTypeRegistry
            builder.For<CmsAttributeTypeRegistry>()
                .KnownAs<IAttributeTypeRegistry>()
                .ScopedAs.Singleton();

            //component registration
            _componentRegistrar.RegisterTasks(builder, typeFinder);
            _componentRegistrar.RegisterTreeControllers(builder, typeFinder);
            _componentRegistrar.RegisterPropertyEditors(builder, typeFinder);
            _componentRegistrar.RegisterParameterEditors(builder, typeFinder);
            _componentRegistrar.RegisterEditorControllers(builder, typeFinder);
            _componentRegistrar.RegisterMenuItems(builder, typeFinder);
            _componentRegistrar.RegisterSurfaceControllers(builder, typeFinder);
            _componentRegistrar.RegisterDashboardFilters(builder, typeFinder);
            _componentRegistrar.RegisterDashboardMatchRules(builder, typeFinder);
            _componentRegistrar.RegisterPermissions(builder, typeFinder);
            _componentRegistrar.RegisterMacroEngines(builder, typeFinder);

            //register the registrations
            builder.For<ComponentRegistrations>().KnownAsSelf();

            //register task manager
            builder.For<ApplicationTaskManager>().KnownAsSelf().ScopedAs.Singleton();
          
            //register our model mappings and resolvers
            builder.AddDependencyDemandBuilder(new ModelMappingsDemandBuilder());

            //TODO: More stuff should happen with the TextManager here (e.g. db access and whatnot)
            //The user may later override settings, most importantly the LocalizationConfig.CurrentTextManager delegate to implement different environments
            //The text manager is assumed to be set up by the framework
            var textManager = LocalizationConfig.TextManager;
            LocalizationWebConfig.ApplyDefaults<TWebApp>(textManager, overridesPath: "~/App_Data/Rebel/LocalizationEntries.xml");
            LocalizationWebConfig.SetupMvcDefaults(setupMetadata: false);

            //The name of the assembly that contains common texts
            textManager.FallbackNamespaces.Add("Rebel.Cms.Web");

            OnContainerBuildingComplete(new ContainerBuilderEventArgs(builder));
        }

        #endregion
    }
}