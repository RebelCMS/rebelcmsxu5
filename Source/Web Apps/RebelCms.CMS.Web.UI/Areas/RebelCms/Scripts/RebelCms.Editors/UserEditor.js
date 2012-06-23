﻿/// <reference path="/Areas/RebelCms/Scripts/RebelCms.System/NamespaceManager.js" />
/// <reference path="/Areas/RebelCms/Modules/RebelCmsTabs/RebelCmsTabs.js" />

RebelCms.System.registerNamespace("RebelCms.Editors");

(function ($, Base) {

    RebelCms.Editors.UserEditor = Base.extend({
        
        init: function (o) {

            var _opts = $.extend({
                //the hidden field to track the active tab index
                activeTabIndexField: true,
                //the active tab index to show on load
                activeTabIndex: ""
            }, o);

            //override the default index if it's zero and the query string exists
            if ($u.Sys.QueryStringHelper.getQueryStringValue("tabindex")) {
                _opts.activeTabIndex = $u.Sys.QueryStringHelper.getQueryStringValue("tabindex");
            }
            
            //create the tabs			
            $("#tabs").RebelCmsTabs({
                content: "#editorContent",
                activeTabIndex: _opts.activeTabIndex,
                activeTabIndexField: _opts.activeTabIndexField
            });
            
            // Force app tray to reload
            $u.Sys.ClientApiManager.getApp().refreshAppTray();

            //create the toolbar UI Element panel
            $("#editorBar .container").UIPanel("Default");
                
        }
        
    }, {
        
        _instance: null,
        
        // Singleton accessor
        getInstance: function () {
            if(this._instance == null)
                this._instance = new RebelCms.Editors.UserEditor();
            return this._instance;
        }
        
    });

})(jQuery, base2.Base);