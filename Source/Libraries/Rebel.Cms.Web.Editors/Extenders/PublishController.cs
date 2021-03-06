﻿using System;
using System.Web.Mvc;
using Rebel.Cms.Web.Caching;
using Rebel.Cms.Web.Context;
using Rebel.Cms.Web.Model.BackOffice.Editors;
using Rebel.Cms.Web.Mvc;
using Rebel.Cms.Web.Mvc.ActionFilters;
using Rebel.Cms.Web.Mvc.ViewEngines;
using Rebel.Framework;
using Rebel.Framework.Localization;
using Rebel.Framework.Persistence;
using Rebel.Framework.Persistence.Model;
using Rebel.Framework.Persistence.Model.Constants;
using Rebel.Framework.Persistence.Model.Constants.AttributeDefinitions;
using Rebel.Framework.Persistence.Model.Versioning;
using Rebel.Framework.Security;
using Rebel.Hive;
using Rebel.Hive.RepositoryTypes;

namespace Rebel.Cms.Web.Editors.Extenders
{
    /// <summary>
    /// Used for the publish dialog
    /// </summary> 
    public class PublishController : ContentControllerExtenderBase
    {
        public PublishController(IBackOfficeRequestContext requestContext)
            : base(requestContext)
        {
        }

        /// <summary>
        /// Displays the publish dialog
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        [RebelAuthorize(Permissions = new[] { FixedPermissionIds.Publish })] 
        public ActionResult Publish(HiveId? id)
        {
            if (id.IsNullValueOrEmpty()) return HttpNotFound();

            using (var uow = Hive.Create<IContentStore>())
            {
                var contentEntity = uow.Repositories.Revisions.GetLatestRevision<TypedEntity>(id.Value);
                if (contentEntity == null)
                    throw new ArgumentException(string.Format("No entity found for id: {0} on action Publish", id));

                return View(new PublishModel
                    {
                        Id = contentEntity.Item.Id,
                        Name = contentEntity.Item.GetAttributeValueAsString(NodeNameAttributeDefinition.AliasValue, "UrlName")
                    });
            }
        }

        /// <summary>
        /// Handles the ajax request for the publish dialog
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost]
        [ActionName("Publish")]
        //[RebelAuthorize(Permissions = new[] { FixedPermissionIds.Publish })] 
        public JsonResult PublishForm(PublishModel model)
        {
            if (!TryValidateModel(model))
            {
                return ModelState.ToJsonErrors();
            }

            using (var uow = Hive.Create<IContentStore>())
            {
                var cacheRecycler = new CacheRecycler(Request.Url.GetLeftPart(UriPartial.Authority),
                                                  BackOfficeRequestContext.Application.FrameworkContext);

                
                var contentEntity = uow.Repositories.Revisions.GetLatestRevision<TypedEntity>(model.Id);
                if (contentEntity == null)
                    throw new ArgumentException(string.Format("No entity found for id: {0} on action PublishForm", model.Id));

                //get its children recursively
                if (model.IncludeChildren)
                {
                    // Get all descendents
                    var descendents = uow.Repositories.GetDescendentRelations(model.Id, FixedRelationTypes.DefaultRelationType);

                    foreach (var descendent in descendents)
                    {
                        //get the revision 
                        var revisionEntity = uow.Repositories.Revisions.GetLatestRevision<TypedEntity>(descendent.DestinationId);

                        //publish it if it's already published or if the user has specified to publish unpublished content
                        if (revisionEntity != null && (revisionEntity.MetaData.StatusType.Alias == FixedStatusTypes.Published.Alias) || model.IncludeUnpublishedChildren)
                        {
                            var publishRevision = revisionEntity.CopyToNewRevision(FixedStatusTypes.Published);
                            uow.Repositories.Revisions.AddOrUpdate(publishRevision);
                            
                            cacheRecycler.RecycleCacheFor(revisionEntity.Item);
                        }
                    }
                }

                //publish this node
                var toPublish = contentEntity.CopyToNewRevision(FixedStatusTypes.Published);
                uow.Repositories.Revisions.AddOrUpdate(toPublish);

                var path = uow.Repositories.GetEntityPaths(model.Id, FixedRelationTypes.DefaultRelationType);

                //save
                uow.Complete();

                cacheRecycler.RecycleCacheFor(toPublish.Item);

                var contentViewModel = BackOfficeRequestContext.Application.FrameworkContext.TypeMappers.Map<Revision<TypedEntity>, ContentEditorModel>(toPublish);

                Notifications.Add(new NotificationMessage(
                                      model.IncludeChildren
                                          ? "Publish.ChildrenSuccess.Message".Localize(this, new { contentViewModel.Name }, encode: false)
                                          : "Publish.SingleSuccess.Message".Localize(this, new { contentViewModel.Name }, encode: false),
                                      "Publish.Title".Localize(this), NotificationType.Success));
                return new CustomJsonResult(new
                    {
                        success = true,
                        notifications = Notifications,
                        path = path.ToJson(),
                        msg = model.IncludeChildren
                                  ? "Publish.ChildrenSuccess.Message".Localize(this, new { contentViewModel.Name }, encode: false)
                                  : "Publish.SingleSuccess.Message".Localize(this, new { contentViewModel.Name }, encode: false)
                    }.ToJsonString);

            }
        }
    }
}
