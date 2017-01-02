using System;
using System.Collections.Generic;
using System.Configuration;
using System.Security.Permissions;
using System.Security.Principal;
using System.Text;
using System.Web;
using System.Web.Security;
using MyFightTracker.Services;

namespace MyFightTracker.Security
{
	[AspNetHostingPermission(SecurityAction.LinkDemand, Level=AspNetHostingPermissionLevel.Minimal)]
    public partial class ExportAuthenticationModule : ExportAuthenticationModuleBase
    {
    }
    
    public class ExportAuthenticationModuleBase : IHttpModule
    {
        
        void IHttpModule.Init(HttpApplication context)
        {
            context.AuthenticateRequest += new EventHandler(this.contextAuthenticateRequest);
            context.EndRequest += new EventHandler(this.contextEndRequest);
        }
        
        void IHttpModule.Dispose()
        {
        }
        
        private void contextEndRequest(object sender, EventArgs e)
        {
            HttpApplication app = ((HttpApplication)(sender));
            if (app.Response.StatusCode == 401)
            	RequestAuthentication(app);
        }
        
        private void contextAuthenticateRequest(object sender, EventArgs e)
        {
            HttpApplication app = ((HttpApplication)(sender));
            ApplicationServices appServices = new ApplicationServices();
            if (!(appServices.RequiresAuthentication(app.Context.Request)))
            	return;
            if (appServices.AuthenticateRequest(app.Context))
            	return;
            HttpCookie c = app.Request.Cookies[FormsAuthentication.FormsCookieName];
            if (c != null)
            {
                FormsAuthenticationTicket t = FormsAuthentication.Decrypt(c.Value);
                if (!(String.IsNullOrEmpty(t.Name)))
                	return;
            }
            string authorization = app.Request.Headers["Authorization"];
            if (String.IsNullOrEmpty(authorization))
            	RequestAuthentication(app);
            else
            	if (authorization.StartsWith("Basic", StringComparison.CurrentCultureIgnoreCase))
                	ValidateUserIdentity(app, authorization);
                else
                	RequestAuthentication(app);
        }
        
        private void RequestAuthentication(HttpApplication app)
        {
            ApplicationServices appServices = new ApplicationServices();
            app.Response.AppendHeader("WWW-Authenticate", String.Format("Basic realm=\"{0}\"", appServices.Realm));
            app.Response.StatusCode = 401;
            app.CompleteRequest();
        }
        
        private void ValidateUserIdentity(HttpApplication app, string authorization)
        {
            string[] login = Encoding.Default.GetString(Convert.FromBase64String(authorization.Substring(6))).Split(new char[] {
                        ':'}, 2);
            if (Membership.ValidateUser(login[0], login[1]))
            	app.Context.User = new RolePrincipal(new FormsIdentity(new FormsAuthenticationTicket(login[0], false, 10)));
            else
            {
                app.Response.StatusCode = 401;
                app.Response.StatusDescription = "Access Denied";
                app.Response.Write("Access denied. Please enter a valid user name and password.");
                app.CompleteRequest();
            }
        }
    }
}
