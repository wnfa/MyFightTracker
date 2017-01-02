using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Caching;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.Security;
using System.Web.SessionState;
using System.Web.Configuration;
using System.IO.Compression;
using MyFightTracker.Data;
using System.Web.Routing;
using System.Drawing;
using System.Drawing.Imaging;
using MyFightTracker.Handlers;
using MyFightTracker.Web;

namespace MyFightTracker.Services
{
	public class ServiceRequestError
    {
        
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private string _exceptionType;
        
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private string _message;
        
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private string _stackTrace;
        
        public string ExceptionType
        {
            get
            {
                return this._exceptionType;
            }
            set
            {
                this._exceptionType = value;
            }
        }
        
        public string Message
        {
            get
            {
                return this._message;
            }
            set
            {
                this._message = value;
            }
        }
        
        public string StackTrace
        {
            get
            {
                return this._stackTrace;
            }
            set
            {
                this._stackTrace = value;
            }
        }
    }
    
    public class WorkflowResources
    {
        
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private SortedDictionary<string, string> _staticResources;
        
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private List<Regex> _dynamicResources;
        
        public WorkflowResources()
        {
            _staticResources = new SortedDictionary<string, string>();
            _dynamicResources = new List<Regex>();
        }
        
        public SortedDictionary<string, string> StaticResources
        {
            get
            {
                return this._staticResources;
            }
            set
            {
                this._staticResources = value;
            }
        }
        
        public List<Regex> DynamicResources
        {
            get
            {
                return this._dynamicResources;
            }
            set
            {
                this._dynamicResources = value;
            }
        }
    }
    
    public partial class WorkflowRegister : WorkflowRegisterBase
    {
    }
    
    public class WorkflowRegisterBase
    {
        
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private SortedDictionary<string, WorkflowResources> _resources;
        
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private SortedDictionary<string, List<string>> _roleRegister;
        
        public WorkflowRegisterBase()
        {
            // initialize system workflows
            _resources = new SortedDictionary<string, WorkflowResources>();
            RegisterBuiltinWorkflowResources();
            foreach (SiteContentFile w in ApplicationServices.Current.ReadSiteContent("sys/workflows%", "%"))
            {
                string text = w.Text;
                if (!(String.IsNullOrEmpty(text)))
                {
                    WorkflowResources wr = null;
                    if (!(Resources.TryGetValue(w.PhysicalName, out wr)))
                    {
                        wr = new WorkflowResources();
                        Resources[w.PhysicalName] = wr;
                    }
                    foreach (string s in text.Split(new char[] {
                                '\n'}, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string query = s.Trim();
                        if (!(String.IsNullOrEmpty(query)))
                        	if (s.StartsWith("regex "))
                            {
                                string regexQuery = s.Substring(6).Trim();
                                if (!(String.IsNullOrEmpty(regexQuery)))
                                	try
                                    {
                                        wr.DynamicResources.Add(new Regex(regexQuery, RegexOptions.IgnoreCase));
                                    }
                                    catch (Exception )
                                    {
                                    }
                            }
                            else
                            	wr.StaticResources[query.ToLower()] = query;
                    }
                }
            }
            // read "role" workflows from the register
            _roleRegister = new SortedDictionary<string, List<string>>();
            foreach (SiteContentFile rr in ApplicationServices.Current.ReadSiteContent("sys/register/roles%", "%"))
            {
                string text = rr.Text;
                if (!(String.IsNullOrEmpty(text)))
                {
                    List<string> workflows = null;
                    if (!(RoleRegister.TryGetValue(rr.PhysicalName, out workflows)))
                    {
                        workflows = new List<string>();
                        RoleRegister[rr.PhysicalName] = workflows;
                    }
                    foreach (string s in text.Split(new char[] {
                                '\n',
                                ','}, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string name = s.Trim();
                        if (!(String.IsNullOrEmpty(name)))
                        	workflows.Add(name);
                    }
                }
            }
        }
        
        public SortedDictionary<string, WorkflowResources> Resources
        {
            get
            {
                return this._resources;
            }
            set
            {
                this._resources = value;
            }
        }
        
        public SortedDictionary<string, List<string>> RoleRegister
        {
            get
            {
                return this._roleRegister;
            }
            set
            {
                this._roleRegister = value;
            }
        }
        
        public List<string> UserWorkflows
        {
            get
            {
                List<string> workflows = ((List<string>)(HttpContext.Current.Items["WorkflowRegister_UserWorkflows"]));
                if (workflows == null)
                {
                    workflows = new List<string>();
                    IIdentity identity = HttpContext.Current.User.Identity;
                    if (identity.IsAuthenticated)
                    	foreach (SiteContentFile urf in ApplicationServices.Current.ReadSiteContent("sys/register/users%", identity.Name))
                        {
                            string text = urf.Text;
                            if (!(String.IsNullOrEmpty(text)))
                            	foreach (string s in text.Split(new char[] {
                                            '\n',
                                            ','}, StringSplitOptions.RemoveEmptyEntries))
                                {
                                    string name = s.Trim();
                                    if (!(String.IsNullOrEmpty(name)) && !(workflows.Contains(name)))
                                    	workflows.Add(name);
                                }
                        }
                    // enumerate role workflows
                    bool isAuthenticated = HttpContext.Current.User.Identity.IsAuthenticated;
                    foreach (string role in RoleRegister.Keys)
                    	if ((((role == "?") && !(isAuthenticated)) || ((role == "*") && isAuthenticated)) || DataControllerBase.UserIsInRole(role))
                        	foreach (string name in RoleRegister[role])
                            	if (!(workflows.Contains(name)))
                                	workflows.Add(name);
                    HttpContext.Current.Items["WorkflowRegister_UserWorkflows"] = workflows;
                }
                return workflows;
            }
        }
        
        public bool Enabled
        {
            get
            {
                return (_resources.Count > 0);
            }
        }
        
        public static bool IsEnabled
        {
            get
            {
                if (!(ApplicationServices.IsSiteContentEnabled))
                	return false;
                WorkflowRegister wr = WorkflowRegister.GetCurrent();
                return ((wr != null) && wr.Enabled);
            }
        }
        
        public virtual int CacheDuration
        {
            get
            {
                return 30;
            }
        }
        
        protected virtual void RegisterBuiltinWorkflowResources()
        {
        }
        
        public static bool Allows(string fileName)
        {
            if (!(ApplicationServices.IsSiteContentEnabled))
            	return false;
            WorkflowRegister wr = WorkflowRegister.GetCurrent(fileName);
            if ((wr == null) || !(wr.Enabled))
            	return false;
            return wr.IsMatch(fileName);
        }
        
        public bool IsMatch(string physicalPath, string physicalName)
        {
            string fileName = physicalPath;
            if (String.IsNullOrEmpty(fileName))
            	fileName = physicalName;
            else
            	fileName = ((fileName + "/") 
                            + physicalName);
            return IsMatch(fileName);
        }
        
        public bool IsMatch(string fileName)
        {
            fileName = fileName.ToLower();
            List<string> activeWorkflows = UserWorkflows;
            foreach (string workflow in activeWorkflows)
            {
                WorkflowResources resourceList = null;
                if (Resources.TryGetValue(workflow, out resourceList))
                {
                    if (resourceList.StaticResources.ContainsKey(fileName))
                    	return true;
                    foreach (Regex re in resourceList.DynamicResources)
                    	if (re.IsMatch(fileName))
                        	return true;
                }
            }
            return false;
        }
        
        public static WorkflowRegister GetCurrent()
        {
            return GetCurrent(null);
        }
        
        public static WorkflowRegister GetCurrent(string relativePath)
        {
            if ((relativePath != null) && (relativePath.StartsWith("sys/workflows") || relativePath.StartsWith("sys/register")))
            	return null;
            string key = "WorkflowRegister_Current";
            HttpContext context = HttpContext.Current;
            WorkflowRegister instance = ((WorkflowRegister)(context.Items[key]));
            if (instance == null)
            {
                instance = ((WorkflowRegister)(context.Cache[key]));
                if (instance == null)
                {
                    instance = new WorkflowRegister();
                    context.Cache.Add(key, instance, null, DateTime.Now.AddSeconds(instance.CacheDuration), Cache.NoSlidingExpiration, CacheItemPriority.AboveNormal, null);
                }
                context.Items[key] = instance;
            }
            return instance;
        }
    }
    
    public enum SiteContentFields
    {
        
        SiteContentId,
        
        DataFileName,
        
        DataContentType,
        
        Path,
        
        Data,
        
        Roles,
        
        Users,
        
        Text,
        
        CacheProfile,
        
        RoleExceptions,
        
        UserExceptions,
        
        Schedule,
        
        ScheduleExceptions,
    }
    
    public class SiteContentFile
    {
        
        private object _id;
        
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private string _name;
        
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private string _path;
        
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private string _contentType;
        
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private byte[] _data;
        
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private string _physicalName;
        
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private string _error;
        
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private string _schedule;
        
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private string _scheduleExceptions;
        
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private string _cacheProfile;
        
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private int _cacheDuration;
        
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private HttpCacheability _cacheLocation;
        
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private string[] _cacheVaryByParams;
        
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private string[] _cacheVaryByHeaders;
        
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private bool _cacheNoStore;
        
        public SiteContentFile()
        {
            this.CacheLocation = HttpCacheability.NoCache;
        }
        
        public object Id
        {
            get
            {
                return _id;
            }
            set
            {
                if ((value != null) && (value.GetType() == typeof(byte[])))
                	value = new Guid(((byte[])(value)));
                _id = value;
            }
        }
        
        public string Name
        {
            get
            {
                return this._name;
            }
            set
            {
                this._name = value;
            }
        }
        
        public string Path
        {
            get
            {
                return this._path;
            }
            set
            {
                this._path = value;
            }
        }
        
        public string ContentType
        {
            get
            {
                return this._contentType;
            }
            set
            {
                this._contentType = value;
            }
        }
        
        public byte[] Data
        {
            get
            {
                return this._data;
            }
            set
            {
                this._data = value;
            }
        }
        
        public string PhysicalName
        {
            get
            {
                return this._physicalName;
            }
            set
            {
                this._physicalName = value;
            }
        }
        
        public string Error
        {
            get
            {
                return this._error;
            }
            set
            {
                this._error = value;
            }
        }
        
        public string Schedule
        {
            get
            {
                return this._schedule;
            }
            set
            {
                this._schedule = value;
            }
        }
        
        public string ScheduleExceptions
        {
            get
            {
                return this._scheduleExceptions;
            }
            set
            {
                this._scheduleExceptions = value;
            }
        }
        
        public string CacheProfile
        {
            get
            {
                return this._cacheProfile;
            }
            set
            {
                this._cacheProfile = value;
            }
        }
        
        public int CacheDuration
        {
            get
            {
                return this._cacheDuration;
            }
            set
            {
                this._cacheDuration = value;
            }
        }
        
        public HttpCacheability CacheLocation
        {
            get
            {
                return this._cacheLocation;
            }
            set
            {
                this._cacheLocation = value;
            }
        }
        
        public string[] CacheVaryByParams
        {
            get
            {
                return this._cacheVaryByParams;
            }
            set
            {
                this._cacheVaryByParams = value;
            }
        }
        
        public string[] CacheVaryByHeaders
        {
            get
            {
                return this._cacheVaryByHeaders;
            }
            set
            {
                this._cacheVaryByHeaders = value;
            }
        }
        
        public bool CacheNoStore
        {
            get
            {
                return this._cacheNoStore;
            }
            set
            {
                this._cacheNoStore = value;
            }
        }
        
        public string Text
        {
            get
            {
                if ((this.Data != null) && (!(String.IsNullOrEmpty(this.ContentType)) && this.ContentType.StartsWith("text/")))
                	return Encoding.UTF8.GetString(this.Data);
                return null;
            }
            set
            {
                if (value == null)
                	_data = null;
                else
                {
                    _data = Encoding.UTF8.GetBytes(value);
                    _contentType = "text/plain";
                }
            }
        }
        
        public bool IsText
        {
            get
            {
                return ((_contentType != null) && (_contentType.StartsWith("text/") || (_contentType == "application/javascript")));
            }
        }
        
        public override string ToString()
        {
            return String.Format("{0}/{1}", Path, Name);
        }
    }
    
    public class SiteContentFileList : List<SiteContentFile>
    {
    }
    
    public partial class ApplicationServices : EnterpriseApplicationServices
    {
        
        public static String HomePageUrl
        {
            get
            {
                return Create().UserHomePageUrl();
            }
        }
        
        public static void Initialize()
        {
            Create().RegisterServices();
        }
        
        public static bool Login(string username, string password, bool createPersistentCookie)
        {
            return Create().UserLogin(username, password, createPersistentCookie);
        }
        
        public static void Logout()
        {
            Create().UserLogout();
        }
        
        public static string[] Roles()
        {
            return Create().UserRoles();
        }
    }
    
    public class ApplicationServicesBase
    {
        
        public static bool EnableMobileClient = true;
        
        public static string DesignerPort = String.Empty;
        
        private static bool _enableMinifiedCss;
        
        public static Regex NameValueListRegex = new Regex("^\\s*(?\'Name\'\\w+)\\s*=\\s*(?\'Value\'[\\S\\s]+?)\\s*$", RegexOptions.Multiline);
        
        public static Regex SystemResourceRegex = new Regex("~/((sys/)|(views/)|(site\\b))", RegexOptions.IgnoreCase);
        
        private static string _mapsApiIdentifier;
        
        private string _userTheme;
        
        public static Regex CssUrlRegex = new Regex("(?\'Header\'\\burl\\s*\\(\\s*(\\&quot;|\\\')?)(?\'Name\'\\w+)(?\'Symbol\'\\S)");
        
        public static Regex ViewPageCompressRegex = new Regex("((\"(DefaultValue)\"\\:(\"[\\s\\S]*?\"))|(\"(Items|Pivots|Fields|Views|ActionGroups|Categ" +
                "ories|Filter|Expressions)\"\\:(\\[\\]))|(\"(Len|CategoryIndex|Rows|Columns|Search|Ite" +
                "msPageSize|Aggregate|OnDemandStyle|TextMode|MaskType|AutoCompletePrefixLength|Da" +
                "taViewPageSize|PageOffset)\"\\:(0))|(\"(CausesValidation|AllowQBE|AllowSorting|Form" +
                "atOnClient|HtmlEncode|RequiresMetaData|RequiresRowCount|ShowInSelector)\"\\:(true)" +
                ")|(\"(IsPrimaryKey|ReadOnly|HasDefaultValue|Hidden|AllowLEV|AllowNulls|OnDemand|I" +
                "sMirror|Calculated|CausesCalculate|IsVirtual|AutoSelect|SearchOnStart|ShowInSumm" +
                "ary|ItemsLetters|WhenKeySelected|RequiresSiteContentText|RequiresPivot|RequiresA" +
                "ggregates|InTransaction|NewColumn|Floating|Collapsed|SupportsCaching|AllowDistin" +
                "ctFieldInFilter|Flat|RequiresMetaData|RequiresRowCount|(DataView(ShowInSummary|S" +
                "howActionBar|ShowDescription|ShowViewSelector|ShowModalForms|SearchByFirstLetter" +
                "|SearchOnStart|ShowPager|ShowPageSize|ShowSearchBar|ShowQuickFind|ShowRowNumber|" +
                "AutoHighlightFirstRow|AutoSelectFirstRow)))\"\\:(false))|(\"(AliasName|Tag|FooterTe" +
                "xt|ToolTip|Watermark|DataFormatString|Copy|HyperlinkFormatString|SourceFields|Se" +
                "archOptions|ItemsDataController|ItemsTargetController|ItemsDataView|ItemsDataVal" +
                "ueField|ItemsDataTextField|ItemsStyle|ItemsNewDataView|OnDemandHandler|Mask|Cont" +
                "extFields|Formula|Configuration|Editor|ItemsDescription|Group|CommandName|Comman" +
                "dArgument|HeaderText|Description|CssClass|Confirmation|Notify|Key|WhenLastComman" +
                "dName|WhenLastCommandArgument|WhenClientScript|WhenTag|WhenHRef|WhenView|PivotDe" +
                "finitions|Aggregates|PivotDefinitions|Aggregates|ViewType|LastView|StatusBar|Ico" +
                "ns|LEVs|QuickFindHint|InnerJoinPrimaryKey|SystemFilter|DistinctValueFieldName|Cl" +
                "ientScript|FirstLetters|SortExpression|Template|Tab|Wizard|InnerJoinForeignKey|E" +
                "xpressions|ViewHeaderText|ViewLayout|GroupExpression|(DataView(FilterSource|Cont" +
                "roller|Id|FilterFields|ShowActionBar|ShowActionButtons|MultiSelect)))\"\\:(\"\"|null" +
                "))|(\"Type\":\"String\")),?");
        
        public static Regex ViewPageCompress2Regex = new Regex(",\\}(,|])");
        
        public static bool EnableMinifiedCss
        {
            get
            {
                return _enableMinifiedCss;
            }
            set
            {
                _enableMinifiedCss = value;
            }
        }
        
        public static bool IsSiteContentEnabled
        {
            get
            {
                return !(String.IsNullOrEmpty(SiteContentControllerName));
            }
        }
        
        public static string SiteContentControllerName
        {
            get
            {
                return Create().GetSiteContentControllerName();
            }
        }
        
        public static string[] SiteContentEditors
        {
            get
            {
                return Create().GetSiteContentEditors();
            }
        }
        
        public static string[] SiteContentDevelopers
        {
            get
            {
                return Create().GetSiteContentDevelopers();
            }
        }
        
        public static bool IsContentEditor
        {
            get
            {
                IPrincipal principal = HttpContext.Current.User;
                foreach (string r in Create().GetSiteContentEditors())
                	if (principal.IsInRole(r))
                    	return true;
                return false;
            }
        }
        
        public static bool IsDeveloper
        {
            get
            {
                IPrincipal principal = HttpContext.Current.User;
                foreach (string r in Create().GetSiteContentDevelopers())
                	if (principal.IsInRole(r))
                    	return true;
                return false;
            }
        }
        
        public static bool IsSafeMode
        {
            get
            {
                HttpRequest request = HttpContext.Current.Request;
                Uri test = request.UrlReferrer;
                if (test == null)
                	test = request.Url;
                return ((test == null) && (test.ToString().Contains("_safemode=true") && DataControllerBase.UserIsInRole(SiteContentDevelopers)));
            }
        }
        
        public virtual int ScheduleCacheDuration
        {
            get
            {
                return 20;
            }
        }
        
        public virtual string Realm
        {
            get
            {
                return Name;
            }
        }
        
        public virtual string Name
        {
            get
            {
                return "My Fight Tracker";
            }
        }
        
        public static string MapsApiIdentifier
        {
            get
            {
                if (String.IsNullOrEmpty(_mapsApiIdentifier))
                	_mapsApiIdentifier = WebConfigurationManager.AppSettings["MapsApiIdentifier"];
                return _mapsApiIdentifier;
            }
        }
        
        public virtual int MaxPivotRowCount
        {
            get
            {
                return 250000;
            }
        }
        
        public static ApplicationServices Current
        {
            get
            {
                return Create();
            }
        }
        
        public static bool IsTouchClient
        {
            get
            {
                object isMobile = HttpContext.Current.Items["ApplicationServices_IsTouchClient"];
                if (isMobile == null)
                {
                    isMobile = ClientIsUsingTouchUI();
                    HttpContext.Current.Items["ApplicationServices_IsTouchClient"] = isMobile;
                }
                return ((bool)(isMobile));
            }
        }
        
        public virtual string UserTheme
        {
            get
            {
                if (String.IsNullOrEmpty(_userTheme))
                {
                    if (HttpContext.Current != null)
                    {
                        HttpCookie themeCookie = HttpContext.Current.Request.Cookies[(".COTTHEME" + BusinessRules.UserName)];
                        if (themeCookie != null)
                        	_userTheme = themeCookie.Value;
                    }
                    if (String.IsNullOrEmpty(_userTheme))
                    	_userTheme = "Dark";
                    _userTheme = _userTheme.Replace(" ", String.Empty);
                }
                return _userTheme;
            }
            set
            {
                _userTheme = value;
            }
        }
        
        public virtual string GetNavigateUrl()
        {
            return null;
        }
        
        public static void VerifyUrl()
        {
            string navigateUrl = Create().GetNavigateUrl();
            if (!(String.IsNullOrEmpty(navigateUrl)))
            {
                HttpContext current = HttpContext.Current;
                if (!(VirtualPathUtility.ToAbsolute(navigateUrl).Equals(current.Request.RawUrl, StringComparison.CurrentCultureIgnoreCase)))
                	current.Response.Redirect(navigateUrl);
            }
        }
        
        public virtual void RegisterServices()
        {
            CreateStandardMembershipAccounts();
            RouteCollection routes = RouteTable.Routes;
            RegisterIgnoredRoutes(routes);
            RegisterContentServices(RouteTable.Routes);
            // Find Designer Port
            try
            {
                string configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "IISExpress\\config\\applicationhost.config");
                if (File.Exists(configPath))
                {
                    string content = File.ReadAllText(configPath);
                    Match m = Regex.Match(content, "<site name=\"CodeOnTime\".*?bindingInformation=\"\\*:(?\'Port\'\\d+):localhost\"", RegexOptions.Singleline);
                    if (m.Success)
                    	DesignerPort = m.Groups["Port"].Value;
                }
            }
            finally
            {
                // release resources here
            }
        }
        
        public static void Start()
        {
            Current.InstanceStart();
        }
        
        protected virtual void InstanceStart()
        {
            MyFightTracker.Services.ApplicationServices.Initialize();
        }
        
        public static void Stop()
        {
            Current.InstanceStop();
        }
        
        protected virtual void InstanceStop()
        {
        }
        
        public static void SessionStart()
        {
            // The line below will prevent intermittent error “Session state has created a session id,
            // but cannot save it because the response was already flushed by the application.”
            string sessionId = HttpContext.Current.Session.SessionID;
            Current.UserSessionStart();
        }
        
        protected virtual void UserSessionStart()
        {
        }
        
        public static void SessionStop()
        {
            Current.UserSessionStop();
        }
        
        protected virtual void UserSessionStop()
        {
        }
        
        public static void Error()
        {
            HttpContext context = HttpContext.Current;
            if (context != null)
            	Current.HandleError(context, context.Server.GetLastError());
        }
        
        void HandleError(HttpContext context, Exception error)
        {
        }
        
        public virtual void RegisterContentServices(RouteCollection routes)
        {
            GenericRoute.Map(RouteTable.Routes, new PlaceholderHandler(), "placeholder/{FileName}");
            routes.MapPageRoute("SiteContent", "{*url}", "~/Site.aspx");
            routes.MapPageRoute("DataControllerService", "{*url}", AquariumExtenderBase.DefaultServicePath);
        }
        
        public virtual void RegisterIgnoredRoutes(RouteCollection routes)
        {
            routes.Ignore("{handler}.ashx");
            routes.Ignore("favicon.ico");
            routes.Ignore("controlhost.aspx");
            routes.Ignore("charthost.aspx");
            routes.Ignore("{resource}.axd/{*pathInfo}");
            routes.Ignore("daf/{service}/{*methodName}");
            routes.Ignore("app_themes/{themeFolder}/{file}");
            routes.Ignore("{id}/arterySignalR/{*pathInfo}");
            if (!(IsSiteContentEnabled))
            {
                routes.Ignore("images/{*pathInfo}");
                routes.Ignore("documents/{*pathInfo}");
                routes.Ignore("download/{*pathInfo}");
            }
            routes.Ignore("touch/{*pathInfo}");
            routes.Ignore("scripts/{*pathInfo}");
            routes.Ignore("services/{*pathInfo}");
        }
        
        public static SortedDictionary<string, string> LoadContent()
        {
            SortedDictionary<string, string> content = new SortedDictionary<string, string>();
            Create().LoadContent(HttpContext.Current.Request, HttpContext.Current.Response, content);
            string rawContent = null;
            if (content.TryGetValue("File", out rawContent))
            {
                // find the head
                Match headMatch = Regex.Match(rawContent, "<head>([\\s\\S]+?)</head>");
                if (headMatch.Success)
                {
                    string head = headMatch.Groups[1].Value;
                    head = Regex.Replace(head, "\\s*<meta charset=\".+\"\\s*/?>\\s*", String.Empty);
                    content["Head"] = Regex.Replace(head, "\\s*<title>([\\S\\s]*?)</title>\\s*", String.Empty);
                    // find the title
                    Match titleMatch = Regex.Match(head, "<title>(?\'Title\'[\\S\\s]+?)</title>");
                    if (titleMatch.Success)
                    {
                        string title = titleMatch.Groups["Title"].Value;
                        content["PageTitle"] = title;
                        content["PageTitleContent"] = title;
                    }
                    // find "about"
                    Match aboutMatch = Regex.Match(head, "<meta\\s+name\\s*=\\s*\"description\"\\s+content\\s*=\\s*\"([\\s\\S]+?)\"\\s*/>");
                    if (aboutMatch.Success)
                    	content["About"] = HttpUtility.HtmlDecode(aboutMatch.Groups[1].Value);
                }
                // find the body
                Match bodyMatch = Regex.Match(rawContent, "<body(?\'Attr\'[\\s\\S]*?)>(?\'Body\'[\\s\\S]+?)</body>");
                if (bodyMatch.Success)
                {
                    content["PageContent"] = bodyMatch.Groups["Body"].Value;
                    content["BodyAttributes"] = bodyMatch.Groups["Attr"].Value;
                }
                else
                	content["PageContent"] = rawContent;
            }
            return content;
        }
        
        public virtual string GetSiteContentControllerName()
        {
            return "myfighttracker_site_content";
        }
        
        public virtual string GetSiteContentViewId()
        {
            return "editForm1";
        }
        
        public virtual string[] GetSiteContentEditors()
        {
            return new string[] {
                    "Administrators",
                    "Content Editors",
                    "Developers"};
        }
        
        public virtual string[] GetSiteContentDevelopers()
        {
            return new string[] {
                    "Administrators",
                    "Developers"};
        }
        
        public virtual void AfterAction(ActionArgs args, ActionResult result)
        {
        }
        
        public virtual void BeforeAction(ActionArgs args, ActionResult result)
        {
            if (args.Controller == SiteContentControllerName)
            {
                bool userIsDeveloper = IsDeveloper;
                if ((!(IsContentEditor) || !(userIsDeveloper)) || (args.Values == null))
                	throw new HttpException(403, "Forbidden");
                FieldValue id = args.SelectFieldValueObject(SiteContentFieldName(SiteContentFields.SiteContentId));
                FieldValue path = args.SelectFieldValueObject(SiteContentFieldName(SiteContentFields.Path));
                FieldValue fileName = args.SelectFieldValueObject(SiteContentFieldName(SiteContentFields.DataFileName));
                FieldValue text = args.SelectFieldValueObject(SiteContentFieldName(SiteContentFields.Text));
                // verify "Path" access
                if ((path == null) || (fileName == null))
                	throw new HttpException(403, "Forbidden");
                if (((path.Value != null) && path.Value.ToString().StartsWith("sys/", StringComparison.CurrentCultureIgnoreCase)) && !(userIsDeveloper))
                	throw new HttpException(403, "Forbidden");
                if (((path.OldValue != null) && path.OldValue.ToString().StartsWith("sys/", StringComparison.CurrentCultureIgnoreCase)) && !(userIsDeveloper))
                	throw new HttpException(403, "Forbidden");
                // convert and parse "Text" as needed
                if ((text != null) && args.CommandName != "Delete")
                {
                    string s = Convert.ToString(text.Value);
                    if (s == "$Text")
                    {
                        string fullPath = Convert.ToString(path.Value);
                        if (!(String.IsNullOrEmpty(fullPath)))
                        	fullPath = (fullPath + "/");
                        fullPath = (fullPath + Convert.ToString(fileName.Value));
                        if (!(fullPath.StartsWith("/")))
                        	fullPath = ("/" + fullPath);
                        if (!(fullPath.EndsWith(".html", StringComparison.CurrentCultureIgnoreCase)))
                        	fullPath = (fullPath + ".html");
                        string physicalPath = HttpContext.Current.Server.MapPath(("~" + fullPath));
                        if (!(File.Exists(physicalPath)))
                        {
                            physicalPath = HttpContext.Current.Server.MapPath(("~" + fullPath.Replace("-", String.Empty)));
                            if (!(File.Exists(physicalPath)))
                            	physicalPath = null;
                        }
                        if (!(String.IsNullOrEmpty(physicalPath)))
                        	text.NewValue = File.ReadAllText(physicalPath);
                    }
                }
            }
        }
        
        public virtual string SiteContentFieldName(SiteContentFields field)
        {
            if (field == SiteContentFields.SiteContentId)
            	return "site_content_id";
            if (field == SiteContentFields.DataFileName)
            	return "file_name";
            if (field == SiteContentFields.DataContentType)
            	return "content_type";
            if (field == SiteContentFields.Roles)
            	return "roles";
            if (field == SiteContentFields.Users)
            	return "users";
            if (field == SiteContentFields.CacheProfile)
            	return "cache_profile";
            if (field == SiteContentFields.RoleExceptions)
            	return "role_exceptions";
            if (field == SiteContentFields.UserExceptions)
            	return "user_exceptions";
            if (field == SiteContentFields.Schedule)
            	return "schedule";
            if (field == SiteContentFields.ScheduleExceptions)
            	return "schedule_exceptions";
            return field.ToString();
        }
        
        public virtual string ReadSiteContentString(string relativePath)
        {
            byte[] data = ReadSiteContentBytes(relativePath);
            if (data == null)
            	return null;
            return Encoding.UTF8.GetString(data);
        }
        
        public virtual byte[] ReadSiteContentBytes(string relativePath)
        {
            SiteContentFile f = ReadSiteContent(relativePath);
            if (f == null)
            	return null;
            return f.Data;
        }
        
        public virtual SiteContentFile ReadSiteContent(string relativePath)
        {
            HttpContext context = HttpContext.Current;
            SiteContentFile f = ((SiteContentFile)(context.Items[relativePath]));
            if (f == null)
            	f = ((SiteContentFile)(context.Cache[relativePath]));
            if (f == null)
            {
                string path = relativePath;
                string fileName = relativePath;
                int index = relativePath.LastIndexOf("/");
                if (index >= 0)
                {
                    fileName = path.Substring((index + 1));
                    path = relativePath.Substring(0, index);
                }
                else
                	path = null;
                SiteContentFileList files = ReadSiteContent(path, fileName, 1);
                if (files.Count == 1)
                {
                    f = files[0];
                    context.Items[relativePath] = f;
                    if (f.CacheDuration > 0)
                    	context.Cache.Add(relativePath, f, null, DateTime.Now.AddSeconds(f.CacheDuration), Cache.NoSlidingExpiration, CacheItemPriority.Normal, null);
                }
            }
            return f;
        }
        
        public virtual SiteContentFileList ReadSiteContent(string relativePath, string fileName)
        {
            return ReadSiteContent(relativePath, fileName, Int32.MaxValue);
        }
        
        public virtual SiteContentFileList ReadSiteContent(string relativePath, string fileName, int maxCount)
        {
            SiteContentFileList result = new SiteContentFileList();
            if (IsSafeMode)
            	return result;
            // prepare a filter
            string dataFileNameField = SiteContentFieldName(SiteContentFields.DataFileName);
            string pathField = SiteContentFieldName(SiteContentFields.Path);
            List<string> filter = new List<string>();
            string pathFilter = null;
            if (!(String.IsNullOrEmpty(relativePath)))
            {
                pathFilter = "{0}:={1}";
                int firstWildcardIndex = relativePath.IndexOf("%");
                if (firstWildcardIndex >= 0)
                {
                    int lastWildcardIndex = relativePath.LastIndexOf("%");
                    pathFilter = "{0}:$contains${1}";
                    if (firstWildcardIndex == lastWildcardIndex)
                    	if (firstWildcardIndex == 0)
                        {
                            pathFilter = "{0}:$endswith${1}";
                            relativePath = relativePath.Substring(1);
                        }
                        else
                        	if (lastWildcardIndex == (relativePath.Length - 1))
                            {
                                pathFilter = "{0}:$beginswith${1}";
                                relativePath = relativePath.Substring(0, lastWildcardIndex);
                            }
                }
            }
            else
            	pathFilter = "{0}:=null";
            string fileNameFilter = null;
            if (!(String.IsNullOrEmpty(fileName)) && !((fileName == "%")))
            {
                fileNameFilter = "{0}:={1}";
                int firstWildcardIndex = fileName.IndexOf("%");
                if (firstWildcardIndex >= 0)
                {
                    int lastWildcardIndex = fileName.LastIndexOf("%");
                    fileNameFilter = "{0}:$contains${1}";
                    if (firstWildcardIndex == lastWildcardIndex)
                    	if (firstWildcardIndex == 0)
                        {
                            fileNameFilter = "{0}:$endswith${1}";
                            fileName = fileName.Substring(1);
                        }
                        else
                        	if (lastWildcardIndex == (fileName.Length - 1))
                            {
                                fileNameFilter = "{0}:$beginswith${1}";
                                fileName = fileName.Substring(0, lastWildcardIndex);
                            }
                }
            }
            if (!(String.IsNullOrEmpty(pathFilter)) || !(String.IsNullOrEmpty(fileNameFilter)))
            {
                filter.Add("_match_:$all$");
                if (!(String.IsNullOrEmpty(pathFilter)))
                	filter.Add(String.Format(pathFilter, pathField, DataControllerBase.ValueToString(relativePath)));
                if (fileName != null && !((fileName == "%")))
                {
                    filter.Add(String.Format(fileNameFilter, dataFileNameField, DataControllerBase.ValueToString(fileName)));
                    if (String.IsNullOrEmpty(Path.GetExtension(fileName)) && (String.IsNullOrEmpty(relativePath) || (!(relativePath.StartsWith("sys/", StringComparison.OrdinalIgnoreCase)) || relativePath.StartsWith("sys/controls", StringComparison.OrdinalIgnoreCase))))
                    {
                        filter.Add("_match_:$all$");
                        if (!(String.IsNullOrEmpty(pathFilter)))
                        	filter.Add(String.Format(pathFilter, pathField, DataControllerBase.ValueToString(relativePath)));
                        filter.Add(String.Format(fileNameFilter, dataFileNameField, DataControllerBase.ValueToString((Path.GetFileNameWithoutExtension(fileName).Replace("-", String.Empty) + ".html"))));
                    }
                }
            }
            //  determine user identity
            HttpContext context = HttpContext.Current;
            string userName = String.Empty;
            bool isAuthenticated = false;
            IPrincipal user = context.User;
            if (user != null)
            {
                userName = user.Identity.Name.ToLower();
                isAuthenticated = user.Identity.IsAuthenticated;
            }
            // enumerate site content files
            PageRequest r = new PageRequest();
            r.Controller = GetSiteContentControllerName();
            r.View = GetSiteContentViewId();
            r.RequiresSiteContentText = true;
            r.PageSize = Int32.MaxValue;
            r.Filter = filter.ToArray();
            IDataEngine engine = ControllerFactory.CreateDataEngine();
            DataControllerBase controller = ((DataControllerBase)(engine));
            controller.AllowPublicAccess = true;
            IDataReader reader = engine.ExecuteReader(r);
            SortedDictionary<string, SiteContentFile> blobsToResolve = new SortedDictionary<string, SiteContentFile>();
            // verify optional SiteContent fields
            SortedDictionary<string, string> fieldDictionary = new SortedDictionary<string, string>();
            for (int i = 0; (i < reader.FieldCount); i++)
            {
                string fieldName = reader.GetName(i);
                fieldDictionary[fieldName] = fieldName;
            }
            string rolesField = null;
            fieldDictionary.TryGetValue(SiteContentFieldName(SiteContentFields.Roles), out rolesField);
            string usersField = null;
            fieldDictionary.TryGetValue(SiteContentFieldName(SiteContentFields.Users), out usersField);
            string roleExceptionsField = null;
            fieldDictionary.TryGetValue(SiteContentFieldName(SiteContentFields.RoleExceptions), out roleExceptionsField);
            string userExceptionsField = null;
            fieldDictionary.TryGetValue(SiteContentFieldName(SiteContentFields.UserExceptions), out userExceptionsField);
            string cacheProfileField = null;
            fieldDictionary.TryGetValue(SiteContentFieldName(SiteContentFields.CacheProfile), out cacheProfileField);
            string scheduleField = null;
            fieldDictionary.TryGetValue(SiteContentFieldName(SiteContentFields.Schedule), out scheduleField);
            string scheduleExceptionsField = null;
            fieldDictionary.TryGetValue(SiteContentFieldName(SiteContentFields.ScheduleExceptions), out scheduleExceptionsField);
            DataField dataField = controller.CreateViewPage().FindField(SiteContentFieldName(SiteContentFields.Data));
            string blobHandler = dataField.OnDemandHandler;
            WorkflowRegister wr = WorkflowRegister.GetCurrent(relativePath);
            // read SiteContent files
            while (reader.Read())
            {
                // verify user access rights
                bool include = true;
                if (!(String.IsNullOrEmpty(rolesField)))
                {
                    string roles = Convert.ToString(reader[rolesField]);
                    if (!(String.IsNullOrEmpty(roles)) && !((roles == "?")))
                    	if ((roles == "*") && !(isAuthenticated))
                        	include = false;
                        else
                        	if (!(isAuthenticated) || (!((roles == "*")) && !(DataControllerBase.UserIsInRole(roles))))
                            	include = false;
                }
                if (include && !(String.IsNullOrEmpty(usersField)))
                {
                    string users = Convert.ToString(reader[usersField]);
                    if (!(String.IsNullOrEmpty(users)) && (Array.IndexOf(users.ToLower().Split(new char[] {
                                                ','}, StringSplitOptions.RemoveEmptyEntries), userName) == -1))
                    	include = false;
                }
                if (include && !(String.IsNullOrEmpty(roleExceptionsField)))
                {
                    string roleExceptions = Convert.ToString(reader[roleExceptionsField]);
                    if (!(String.IsNullOrEmpty(roleExceptions)) && (isAuthenticated && ((roleExceptions == "*") || DataControllerBase.UserIsInRole(roleExceptions))))
                    	include = false;
                }
                if (include && !(String.IsNullOrEmpty(userExceptionsField)))
                {
                    string userExceptions = Convert.ToString(reader[userExceptionsField]);
                    if (!(String.IsNullOrEmpty(userExceptions)) && !((Array.IndexOf(userExceptions.ToLower().Split(new char[] {
                                                ','}, StringSplitOptions.RemoveEmptyEntries), userName) == -1)))
                    	include = false;
                }
                string physicalName = Convert.ToString(reader[dataFileNameField]);
                string physicalPath = Convert.ToString(reader[SiteContentFieldName(SiteContentFields.Path)]);
                // check if the content object is a part of a workflow
                if (((wr != null) && wr.Enabled) && !(wr.IsMatch(physicalPath, physicalName)))
                	include = false;
                string schedule = null;
                string scheduleExceptions = null;
                // check if the content object is on schedule
                if (include && (String.IsNullOrEmpty(physicalPath) || !(physicalPath.StartsWith("sys/schedules/"))))
                {
                    if (!(String.IsNullOrEmpty(scheduleField)))
                    	schedule = Convert.ToString(reader[scheduleField]);
                    if (!(String.IsNullOrEmpty(scheduleExceptionsField)))
                    	scheduleExceptions = Convert.ToString(reader[scheduleExceptionsField]);
                    if (!(String.IsNullOrEmpty(schedule)) || !(String.IsNullOrEmpty(scheduleExceptions)))
                    {
                        string scheduleStatusKey = String.Format("ScheduleStatus|{0}|{1}", schedule, scheduleExceptions);
                        ScheduleStatus status = ((ScheduleStatus)(context.Items[scheduleStatusKey]));
                        if (status == null)
                        	status = ((ScheduleStatus)(context.Cache[scheduleStatusKey]));
                        bool scheduleStatusChanged = false;
                        if (status == null)
                        {
                            if (!(String.IsNullOrEmpty(schedule)) && !(schedule.Contains("+")))
                            	schedule = ReadSiteContentString(("sys/schedules%/" + schedule));
                            if (!(String.IsNullOrEmpty(scheduleExceptions)) && !(scheduleExceptions.Contains("+")))
                            	scheduleExceptions = ReadSiteContentString(("sys/schedules%/" + scheduleExceptions));
                            if (!(String.IsNullOrEmpty(schedule)) || !(String.IsNullOrEmpty(scheduleExceptions)))
                            	status = Scheduler.Test(schedule, scheduleExceptions);
                            else
                            {
                                status = new ScheduleStatus();
                                status.Success = true;
                                status.NextTestDate = DateTime.MaxValue;
                            }
                            context.Items[scheduleStatusKey] = status;
                            scheduleStatusChanged = true;
                        }
                        else
                        	if (DateTime.Now > status.NextTestDate)
                            {
                                status = Scheduler.Test(status.Schedule, status.Exceptions);
                                context.Items[scheduleStatusKey] = status;
                                scheduleStatusChanged = true;
                            }
                        if (scheduleStatusChanged)
                        	context.Cache.Add(scheduleStatusKey, status, null, DateTime.Now.AddSeconds(ScheduleCacheDuration), Cache.NoSlidingExpiration, CacheItemPriority.Normal, null);
                        if (!(status.Success))
                        	include = false;
                    }
                }
                // create a file instance
                if (include)
                {
                    string siteContentIdField = SiteContentFieldName(SiteContentFields.SiteContentId);
                    SiteContentFile f = new SiteContentFile();
                    f.Id = reader[siteContentIdField];
                    f.Name = fileName;
                    f.PhysicalName = physicalName;
                    if (String.IsNullOrEmpty(f.Name) || f.Name.Contains("%"))
                    	f.Name = f.PhysicalName;
                    f.Path = physicalPath;
                    f.ContentType = Convert.ToString(reader[SiteContentFieldName(SiteContentFields.DataContentType)]);
                    f.Schedule = schedule;
                    f.ScheduleExceptions = scheduleExceptions;
                    if (!(String.IsNullOrEmpty(cacheProfileField)))
                    {
                        string cacheProfile = Convert.ToString(reader[cacheProfileField]);
                        if (!(String.IsNullOrEmpty(cacheProfile)))
                        {
                            f.CacheProfile = cacheProfile;
                            cacheProfile = ReadSiteContentString(("sys/cache-profiles/" + cacheProfile));
                            if (!(String.IsNullOrEmpty(cacheProfile)))
                            {
                                Match m = NameValueListRegex.Match(cacheProfile);
                                while (m.Success)
                                {
                                    string n = m.Groups["Name"].Value.ToLower();
                                    string v = m.Groups["Value"].Value;
                                    if (n == "duration")
                                    {
                                        int duration = 0;
                                        if (Int32.TryParse(v, out duration))
                                        {
                                            f.CacheDuration = duration;
                                            f.CacheLocation = HttpCacheability.ServerAndPrivate;
                                        }
                                    }
                                    else
                                    	if (n == "location")
                                        	try
                                            {
                                                f.CacheLocation = ((HttpCacheability)(TypeDescriptor.GetConverter(typeof(HttpCacheability)).ConvertFromString(v)));
                                            }
                                            catch (Exception )
                                            {
                                            }
                                        else
                                        	if (n == "varybyheaders")
                                            	f.CacheVaryByHeaders = v.Split(new char[] {
                                                            ',',
                                                            ';'}, StringSplitOptions.RemoveEmptyEntries);
                                            else
                                            	if (n == "varybyparams")
                                                	f.CacheVaryByParams = v.Split(new char[] {
                                                                ',',
                                                                ';'}, StringSplitOptions.RemoveEmptyEntries);
                                                else
                                                	if (n == "nostore")
                                                    	f.CacheNoStore = (v.ToLower() == "true");
                                    m = m.NextMatch();
                                }
                            }
                        }
                    }
                    object textString = reader[SiteContentFieldName(SiteContentFields.Text)];
                    if (DBNull.Value.Equals(textString) || !(f.IsText))
                    {
                        string blobKey = String.Format("{0}=o|{1}", blobHandler, f.Id);
                        if (f.CacheDuration > 0)
                        	f.Data = ((byte[])(HttpContext.Current.Cache[blobKey]));
                        if (f.Data == null)
                        	blobsToResolve[blobKey] = f;
                    }
                    else
                    {
                        if (String.IsNullOrEmpty(f.ContentType))
                        	if (Regex.IsMatch(((string)(textString)), "</\\w+\\s*>"))
                            	f.ContentType = "text/xml";
                            else
                            	f.ContentType = "text/plain";
                        f.Data = Encoding.UTF8.GetBytes(((string)(textString)));
                    }
                    result.Add(f);
                    if (result.Count == maxCount)
                    	break;
                }
            }
            reader.Close();
            foreach (string blobKey in blobsToResolve.Keys)
            {
                SiteContentFile f = blobsToResolve[blobKey];
                // download blob content
                try
                {
                    f.Data = Blob.Read(blobKey);
                    if (f.CacheDuration > 0)
                    	HttpContext.Current.Cache.Add(blobKey, f.Data, null, DateTime.Now.AddSeconds(f.CacheDuration), Cache.NoSlidingExpiration, CacheItemPriority.Normal, null);
                }
                catch (Exception ex)
                {
                    f.Error = ex.Message;
                }
            }
            return result;
        }
        
        public virtual bool IsSystemResource(HttpRequest request)
        {
            return SystemResourceRegex.IsMatch(request.AppRelativeCurrentExecutionFilePath);
        }
        
        public virtual void LoadContent(HttpRequest request, HttpResponse response, SortedDictionary<string, string> content)
        {
            if (IsSystemResource(request))
            	return;
            string text = null;
            bool tryFileSystem = true;
            if (IsSiteContentEnabled)
            {
                string fileName = HttpUtility.UrlDecode(request.Url.Segments[(request.Url.Segments.Length - 1)]);
                string path = request.CurrentExecutionFilePath.Substring(request.ApplicationPath.Length);
                if ((fileName == "/") && String.IsNullOrEmpty(path))
                	fileName = "index";
                else
                {
                    path = path.Substring(0, (path.Length - fileName.Length));
                    if (path.EndsWith("/"))
                    	path = path.Substring(0, (path.Length - 1));
                }
                if (String.IsNullOrEmpty(path))
                	path = null;
                SiteContentFileList files = ReadSiteContent(path, fileName, 1);
                if (files.Count > 0)
                {
                    SiteContentFile f = files[0];
                    if (f.ContentType == "text/html")
                    {
                        text = f.Text;
                        tryFileSystem = false;
                    }
                    else
                    {
                        if (f.CacheDuration > 0)
                        {
                            DateTime expires = DateTime.Now.AddSeconds(f.CacheDuration);
                            response.Cache.SetExpires(expires);
                            response.Cache.SetCacheability(f.CacheLocation);
                            if (f.CacheVaryByParams != null)
                            	foreach (string header in f.CacheVaryByParams)
                                	response.Cache.VaryByParams[header] = true;
                            if (f.CacheVaryByHeaders != null)
                            	foreach (string header in f.CacheVaryByHeaders)
                                	response.Cache.VaryByHeaders[header] = true;
                            if (f.CacheNoStore)
                            	response.Cache.SetNoStore();
                        }
                        response.ContentType = f.ContentType;
                        response.AddHeader("Content-Disposition", ("filename=" + HttpUtility.UrlEncode(f.PhysicalName)));
                        response.OutputStream.Write(f.Data, 0, f.Data.Length);
                        try
                        {
                            response.Flush();
                        }
                        catch (Exception )
                        {
                        }
                        response.End();
                    }
                }
            }
            if (tryFileSystem)
            {
                string fileName = request.PhysicalPath;
                if (!(String.IsNullOrEmpty(Path.GetExtension(fileName))))
                	fileName = Path.Combine(Path.GetDirectoryName(fileName), Path.GetFileNameWithoutExtension(fileName));
                fileName = (fileName + ".html");
                if (File.Exists(fileName))
                	text = File.ReadAllText(fileName);
                else
                	if (Path.GetFileNameWithoutExtension(fileName).Contains("-"))
                    {
                        fileName = Path.Combine(Path.GetDirectoryName(fileName), Path.GetFileName(fileName).Replace("-", String.Empty));
                        if (File.Exists(fileName))
                        	text = File.ReadAllText(fileName);
                    }
                if (text != null)
                	text = Localizer.Replace("Pages", fileName, text);
            }
            if (text != null)
            {
                text = Regex.Replace(text, "<!--[\\s\\S]+?-->\\s*", String.Empty);
                content["File"] = text;
            }
        }
        
        public virtual void CreateStandardMembershipAccounts()
        {
            // Create a separate code file with a definition of the partial class ApplicationServices overriding
            // this method to prevent automatic registration of 'admin' and 'user'. Do not change this file directly.
            RegisterStandardMembershipAccounts();
        }
        
        public virtual bool RequiresAuthentication(HttpRequest request)
        {
            return request.Path.EndsWith("Export.ashx", StringComparison.CurrentCultureIgnoreCase);
        }
        
        public virtual bool AuthenticateRequest(HttpContext context)
        {
            return false;
        }
        
        public virtual bool UserLogin(string username, string password, bool createPersistentCookie)
        {
            if (Membership.ValidateUser(username, password))
            {
                FormsAuthentication.SetAuthCookie(username, createPersistentCookie);
                return true;
            }
            else
            	return false;
        }
        
        public virtual void UserLogout()
        {
            FormsAuthentication.SignOut();
        }
        
        public virtual string[] UserRoles()
        {
            return Roles.GetRolesForUser();
        }
        
        public virtual string UserHomePageUrl()
        {
            if (IsSiteContentEnabled)
            {
                SiteContentFile index = ReadSiteContent("index");
                if (index != null)
                	return HttpContext.Current.Request.ApplicationPath;
            }
            return "~/pages/home";
        }
        
        public static ApplicationServices Create()
        {
            return new ApplicationServices();
        }
        
        public static bool UserIsAuthorizedToAccessResource(string path, string roles)
        {
            return !(Create().ResourceAuthorizationIsRequired(path, roles));
        }
        
        public virtual bool ResourceAuthorizationIsRequired(string path, string roles)
        {
            if (roles == null)
            	roles = String.Empty;
            else
            	roles = roles.Trim();
            bool requiresAuthorization = false;
            bool isAuthenticated = HttpContext.Current.User.Identity.IsAuthenticated;
            if (String.IsNullOrEmpty(roles) && !(isAuthenticated))
            	requiresAuthorization = true;
            if (!(String.IsNullOrEmpty(roles)) && !((roles == "?")))
            	if (roles == "*")
                {
                    if (!(isAuthenticated))
                    	requiresAuthorization = true;
                }
                else
                	if (!(isAuthenticated) || !(DataControllerBase.UserIsInRole(roles)))
                    	requiresAuthorization = true;
            if (path == FormsAuthentication.LoginUrl)
            	requiresAuthorization = false;
            return requiresAuthorization;
        }
        
        public static void RegisterStandardMembershipAccounts()
        {
            MembershipUser admin = Membership.GetUser("admin");
            if ((admin != null) && admin.IsLockedOut)
            	admin.UnlockUser();
            MembershipUser user = Membership.GetUser("user");
            if ((user != null) && user.IsLockedOut)
            	user.UnlockUser();
            if (Membership.GetUser("admin") == null)
            {
                MembershipCreateStatus status;
                admin = Membership.CreateUser("admin", "admin123%", "admin@MyFightTracker.com", "ASP.NET", "Code OnTime", true, out status);
                user = Membership.CreateUser("user", "user123%", "user@MyFightTracker.com", "ASP.NET", "Code OnTime", true, out status);
                Roles.CreateRole("Administrators");
                Roles.CreateRole("Users");
                Roles.AddUserToRole(admin.UserName, "Users");
                Roles.AddUserToRole(user.UserName, "Users");
                Roles.AddUserToRole(admin.UserName, "Administrators");
            }
        }
        
        public static bool ClientIsUsingTouchUI()
        {
            if (!(EnableMobileClient))
            	return false;
            HttpRequest request = HttpContext.Current.Request;
            HttpCookie mobileCookie = request.Cookies["appfactorytouchui"];
            if (mobileCookie != null)
            	return (mobileCookie.Value == "true");
            return true;
        }
        
        public static void RegisterCssLinks(Page p)
        {
            foreach (Control c in p.Header.Controls)
            	if (c is HtmlLink)
                {
                    HtmlLink l = ((HtmlLink)(c));
                    if (l.ID == "MyFightTrackerTheme")
                    	return;
                    if (l.Href.Contains("_Theme_Aquarium.css"))
                    {
                        l.ID = "MyFightTrackerTheme";
                        if (ApplicationServices.IsTouchClient)
                        {
                            string jqmCss = String.Format("jquery.mobile-{0}.min.css", ApplicationServices.JqmVersion);
                            l.Href = ("~/touch/" + jqmCss);
                            HtmlMeta meta = new HtmlMeta();
                            meta.Attributes["name"] = "viewport";
                            meta.Attributes["content"] = "width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no";
                            p.Header.Controls.AddAt(0, meta);
                            bool allowCompression = true;
                            if (ApplicationServices.EnableMinifiedCss && allowCompression)
                            {
                                l.Href = (p.ResolveUrl(string.Format("~/appservices/stylesheet-{0}.min.css", ApplicationServices.Version)) + String.Format("?_t={0}&_cf=", ApplicationServices.Create().UserTheme));
                                l.Attributes["class"] = "app-theme";
                            }
                            else
                            	foreach (string stylesheet in ApplicationServices.TouchUIStylesheets())
                                	if (!(stylesheet.StartsWith("jquery.mobile")) && !(stylesheet.StartsWith("bootstrap")))
                                    {
                                        HtmlLink cssLink = new HtmlLink();
                                        cssLink.Href = String.Format("~/touch/{0}?{1}", stylesheet, ApplicationServices.Version);
                                        cssLink.Attributes["type"] = "text/css";
                                        cssLink.Attributes["rel"] = "stylesheet";
                                        if (stylesheet.Contains("app-themes."))
                                        	cssLink.Attributes["class"] = "app-theme";
                                        p.Header.Controls.Add(cssLink);
                                    }
                            List<Control> removeList = new List<Control>();
                            foreach (Control c2 in p.Header.Controls)
                            	if (c2 is HtmlLink)
                                {
                                    l = ((HtmlLink)(c2));
                                    if (l.Href.Contains("App_Themes/"))
                                    	removeList.Add(l);
                                }
                            foreach (Control c2 in removeList)
                            	p.Header.Controls.Remove(c2);
                            return;
                        }
                        else
                        	if (!(l.Href.Contains("?")))
                            	l.Href = (l.Href + String.Format("?{0}", ApplicationServices.Version));
                        return;
                    }
                }
        }
        
        protected virtual bool AllowTouchUIStylesheet(string name)
        {
            string file = Path.GetFileName(name);
            if (file.StartsWith("app-themes") && !(file.StartsWith(("app-themes." 
                            + (UserTheme.ToLower() + ".")))))
            	return false;
            return true;
        }
        
        public static List<string> TouchUIStylesheets()
        {
            return Create().EnumerateTouchUIStylesheets();
        }
        
        public virtual List<string> EnumerateTouchUIStylesheets()
        {
            List<string> stylesheets = new List<string>();
            stylesheets.Add(string.Format("jquery.mobile-{0}.min.css", ApplicationServices.JqmVersion));
            // enumerate custom css files
            string mobilePath = Path.GetDirectoryName(HttpContext.Current.Server.MapPath("~/touch/"));
            SortedDictionary<string, string> cssFiles = new SortedDictionary<string, string>();
            List<string> minifiedCssFiles = new List<string>();
            foreach (string css in Directory.GetFiles(mobilePath, "*.css"))
            	if (!(css.Contains("jquery.mobile")))
                {
                    cssFiles[css] = css;
                    if (css.EndsWith(".min.css"))
                    	if (EnableMinifiedCss)
                        	minifiedCssFiles.Add((css.Substring(0, (css.Length - 7)) + "css"));
                        else
                        	cssFiles.Remove(css);
                }
            foreach (string css in minifiedCssFiles)
            	cssFiles.Remove(css);
            foreach (string fileName in cssFiles.Keys)
            {
                string cssFile = Path.GetFileName(fileName);
                if (AllowTouchUIStylesheet(fileName))
                	stylesheets.Add(cssFile);
            }
            return stylesheets;
        }
        
        private static string DoReplaceCssUrl(Match m)
        {
            string header = m.Groups["Header"].Value;
            string name = m.Groups["Name"].Value;
            string symbol = m.Groups["Symbol"].Value;
            if ((name == "data") && (symbol == ":"))
            	return m.Value;
            return (header 
                        + ("../touch/" 
                        + (name + symbol)));
        }
        
        public static string CombineTouchUIStylesheets(HttpContext context)
        {
            HttpResponse response = context.Response;
            HttpCachePolicy cache = response.Cache;
            cache.SetCacheability(HttpCacheability.Public);
            cache.VaryByHeaders["User-Agent"] = true;
            cache.SetOmitVaryStar(true);
            cache.SetExpires(DateTime.Now.AddDays(365));
            cache.SetValidUntilExpires(true);
            cache.SetLastModifiedFromFileDependencies();
            // combine scripts
            string contentFramework = context.Request.QueryString["_cf"];
            bool includeBootstrap = (contentFramework == "bootstrap");
            StringBuilder sb = new StringBuilder();
            foreach (string stylesheet in ApplicationServices.TouchUIStylesheets())
            	if (includeBootstrap || !(stylesheet.StartsWith("bootstrap")))
                {
                    string data = File.ReadAllText(HttpContext.Current.Server.MapPath(("~/touch/" + stylesheet)));
                    data = CssUrlRegex.Replace(data, DoReplaceCssUrl);
                    sb.AppendLine(data);
                }
            return sb.ToString();
        }
        
        public static void CompressOutput(HttpContext context, string data)
        {
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;
            string acceptEncoding = request.Headers["Accept-Encoding"];
            if (!(String.IsNullOrEmpty(acceptEncoding)))
            	if (acceptEncoding.Contains("gzip"))
                {
                    response.Filter = new GZipStream(response.Filter, CompressionMode.Compress);
                    response.AppendHeader("Content-Encoding", "gzip");
                }
                else
                	if (acceptEncoding.Contains("deflate"))
                    {
                        response.Filter = new DeflateStream(response.Filter, CompressionMode.Compress);
                        response.AppendHeader("Content-Encoding", "deflate");
                    }
            byte[] output = Encoding.UTF8.GetBytes(data);
            response.ContentEncoding = Encoding.Unicode;
            response.AddHeader("Content-Length", output.Length.ToString());
            response.OutputStream.Write(output, 0, output.Length);
            try
            {
                response.Flush();
            }
            catch (Exception )
            {
            }
        }
        
        public static void HandleServiceRequest(HttpContext context)
        {
            byte[] data = new byte[context.Request.InputStream.Length];
            context.Request.InputStream.Read(data, 0, data.Length);
            string json = Encoding.UTF8.GetString(data);
#pragma warning disable 0618
            System.Web.Script.Serialization.JavaScriptSerializer serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
#pragma warning restore 0618
            object result = null;
            string methodName = context.Request.Path.Substring((((Page)(context.Handler)).ResolveUrl(AquariumExtenderBase.DefaultServicePath).Length + 1));
            if ((String.IsNullOrEmpty(methodName) || (String.IsNullOrEmpty(json) && !(Regex.IsMatch(methodName, "^(Logout|ListAllPermalinks|Roles)$")))) || context.Request.HttpMethod != "POST")
            	context.Response.End();
            object v = serializer.Deserialize<object>(json);
            Dictionary<string, object> args = ((Dictionary<string, object>)(v));
            DataControllerService service = new DataControllerService();
            try
            {
                if (methodName == "GetPage")
                {
                    PageRequest r = ((PageRequest)(serializer.Deserialize<PageRequest>(Regex.Match(json, "\"request\":([\\s\\S]+?)(}$)").Groups[1].Value)));
                    result = service.GetPage(((string)(args["controller"])), ((string)(args["view"])), r);
                }
                else
                	if (methodName == "GetPageList")
                    {
                        List<PageRequest> list = new List<PageRequest>();
                        Match m = Regex.Match(json, "\"requests\":\\[([\\s\\S]+?)\\]}$");
                        if (m.Success)
                        {
                            Match m2 = Regex.Match(m.Groups[1].Value, "({[\\s\\S]+?})(,|$)");
                            while (m2.Success)
                            {
                                list.Add(((PageRequest)(serializer.Deserialize<PageRequest>(m2.Groups[1].Value))));
                                m2 = m2.NextMatch();
                            }
                        }
                        result = service.GetPageList(list.ToArray());
                    }
                    else
                    	if (methodName == "GetListOfValues")
                        {
                            DistinctValueRequest r = ((DistinctValueRequest)(serializer.Deserialize<DistinctValueRequest>(Regex.Match(json, "\"request\":([\\s\\S]+?)(}$)").Groups[1].Value)));
                            result = service.GetListOfValues(((string)(args["controller"])), ((string)(args["view"])), r);
                        }
                        else
                        	if (methodName == "Execute")
                            {
                                ActionArgs a = ((ActionArgs)(serializer.Deserialize<ActionArgs>(Regex.Match(json, "\"args\":([\\s\\S]+?)(}$)").Groups[1].Value)));
                                result = service.Execute(((string)(args["controller"])), ((string)(args["view"])), a);
                            }
                            else
                            	if (methodName == "GetCompletionList")
                                	result = service.GetCompletionList(((string)(args["prefixText"])), ((int)(args["count"])), ((string)(args["contextKey"])));
                                else
                                	if (methodName == "Login")
                                    	result = service.Login(((string)(args["username"])), ((string)(args["password"])), ((bool)(args["createPersistentCookie"])));
                                    else
                                    	if (methodName == "Logout")
                                        	service.Logout();
                                        else
                                        	if (methodName == "Roles")
                                            	result = service.Roles();
                                            else
                                            	if (methodName == "SavePermalink")
                                                	service.SavePermalink(((string)(args["link"])), ((string)(args["html"])));
                                                else
                                                	if (methodName == "EncodePermalink")
                                                    	result = service.EncodePermalink(((string)(args["link"])), ((bool)(args["rooted"])));
                                                    else
                                                    	if (methodName == "ListAllPermalinks")
                                                        	result = service.ListAllPermalinks();
            }
            catch (Exception ex)
            {
                while (ex.InnerException != null)
                	ex = ex.InnerException;
                ServiceRequestError error = new ServiceRequestError();
                error.Message = ex.Message;
                error.ExceptionType = ex.GetType().ToString();
                error.StackTrace = ex.StackTrace;
                result = error;
            }
            if (result != null)
            {
                context.Response.ContentType = "application/json; charset=utf-8";
                string output = String.Format("{{\"d\":{0}}}", serializer.Serialize(result));
                int startIndex = 0;
                int dataIndex = 0;
                int lastIndex = 0;
                int lastLength = output.Length;
                while (true)
                {
                    startIndex = output.IndexOf("{\"Controller\":", lastIndex, StringComparison.Ordinal);
                    dataIndex = output.IndexOf(",\"NewRow\":", lastIndex, StringComparison.Ordinal);
                    if ((startIndex < 0) || (dataIndex < 0))
                    	break;
                    string metadata = (output.Substring(0, startIndex) + ViewPageCompressRegex.Replace(output.Substring(startIndex, (dataIndex - startIndex)), String.Empty));
                    if (metadata.EndsWith(","))
                    	metadata = metadata.Substring(0, (metadata.Length - 1));
                    output = (ViewPageCompress2Regex.Replace(metadata, "}$1") + output.Substring(dataIndex));
                    lastIndex = ((dataIndex + 10) 
                                - (lastLength - output.Length));
                    lastLength = output.Length;
                }
                ApplicationServices.CompressOutput(context, output);
            }
            context.Response.End();
        }
    }
    
    public class AnonymousUserIdentity : IIdentity
    {
        
        string IIdentity.AuthenticationType
        {
            get
            {
                return "None";
            }
        }
        
        bool IIdentity.IsAuthenticated
        {
            get
            {
                return false;
            }
        }
        
        string IIdentity.Name
        {
            get
            {
                return String.Empty;
            }
        }
    }
    
    public partial class ApplicationSiteMapProvider : ApplicationSiteMapProviderBase
    {
    }
    
    public class ApplicationSiteMapProviderBase : System.Web.XmlSiteMapProvider
    {
        
        public override bool IsAccessibleToUser(HttpContext context, SiteMapNode node)
        {
            string device = node["Device"];
            bool isTouchUI = ApplicationServices.IsTouchClient;
            if ((device == "touch") && !(isTouchUI))
            	return false;
            if ((device == "desktop") && isTouchUI)
            	return false;
            return base.IsAccessibleToUser(context, node);
        }
    }
    
    public partial class ApplicationSessionState : ApplicationSessionStateBase
    {
    }
    
    public class ApplicationSessionStateBase : SessionStateStoreProviderBase
    {
        
        public static ApplicationSessionStateBase Current;
        
        private SessionStateSection _config = null;
        
        private string _connectionStringName;
        
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private string _applicationName;
        
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private bool _writeExceptionsToEventLog;
        
        public virtual string ApplicationName
        {
            get
            {
                return this._applicationName;
            }
            set
            {
                this._applicationName = value;
            }
        }
        
        public virtual bool WriteExceptionsToEventLog
        {
            get
            {
                return this._writeExceptionsToEventLog;
            }
            set
            {
                this._writeExceptionsToEventLog = value;
            }
        }
        
        public virtual void DeleteExpiredSessions()
        {
            using (SqlText cmd = CreateSqlText("DELETE FROM aspnet_sessions WHERE expires < @Expires"))
            {
                cmd.AddParameter("Expires", DateTime.Now);
                cmd.ExecuteNonQuery();
            }
        }
        
        public override void Initialize(string name, NameValueCollection config)
        {
            if (config == null)
            	throw new ArgumentNullException("config");
            if (String.IsNullOrEmpty(name))
            	name = "ApplicationSessionState";
            if (String.IsNullOrEmpty(config["description"]))
            {
                config.Remove("description");
                config.Add("description", "Custom Session State Store Provider");
            }
            base.Initialize(name, config);
            _applicationName = System.Web.Hosting.HostingEnvironment.ApplicationVirtualPath;
            System.Configuration.Configuration cfg = WebConfigurationManager.OpenWebConfiguration(ApplicationName);
            _config = ((SessionStateSection)(cfg.GetSection("system.web/sessionState")));
            ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings[config["connectionStringName"]];
            if (settings != null)
            	_connectionStringName = settings.Name;
            _writeExceptionsToEventLog = false;
            if (config["writeExceptionsToEventLog"] != null)
            {
                if (config["writeExceptionsToEventLog"].ToUpper() == "TRUE")
                	_writeExceptionsToEventLog = true;
            }
            Current = this;
        }
        
        public override void Dispose()
        {
        }
        
        public override bool SetItemExpireCallback(SessionStateItemExpireCallback expireCallback)
        {
            return false;
        }
        
        public override void SetAndReleaseItemExclusive(HttpContext context, string id, SessionStateStoreData item, object lockId, bool newItem)
        {
            byte[] sessItems = Serialize(((SessionStateItemCollection)(item.Items)));
            if (newItem)
            {
                DeleteExpiredSessions();
                using (SqlText cmd = CreateSqlText("INSERT INTO aspnet_sessions (session_id, application_name, created, expires, lock" +
                        "_date, lock_id, timeout, locked, session_items, flags) Values(@SessionId, @Appli" +
                        "cationName, @Created, @Expires, @LockDate, @LockId , @Timeout, @Locked, @Session" +
                        "Items, @Flags)"))
                {
                    cmd.AddParameter("SessionId", id);
                    cmd.AddParameter("Expires", DateTime.Now.AddMinutes(((double)(item.Timeout))));
                    cmd.AddParameter("Created", DateTime.Now);
                    cmd.AddParameter("LockDate", DateTime.Now);
                    cmd.AddParameter("LockId", 0);
                    cmd.AddParameter("Timeout", item.Timeout);
                    cmd.AddParameter("Locked", 0);
                    cmd.AddParameter("SessionItems", sessItems);
                    cmd.AddParameter("Flags", 0);
                    cmd.ExecuteNonQuery();
                }
            }
            else
            	using (SqlText cmd = CreateSqlText("UPDATE aspnet_sessions SET expires = @Expires, session_items = @SessionItems, loc" +
                        "ked = @Locked WHERE session_id = @SessionId AND application_name = @ApplicationN" +
                        "ame AND lock_id = @LockId"))
                {
                    cmd.AddParameter("SessionId", id);
                    cmd.AddParameter("Expires", DateTime.Now.AddMinutes(((double)(item.Timeout))));
                    cmd.AddParameter("SessionItems", sessItems);
                    cmd.AddParameter("Locked", 0);
                    cmd.AddParameter("LockId", lockId);
                    cmd.ExecuteNonQuery();
                }
        }
        
        public override SessionStateStoreData GetItem(HttpContext context, string id, out bool locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actionFlags)
        {
            return GetSessionStoreItem(false, context, id, out locked, out lockAge, out lockId, out actionFlags);
        }
        
        public override SessionStateStoreData GetItemExclusive(HttpContext context, string id, out bool locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actionFlags)
        {
            return GetSessionStoreItem(true, context, id, out locked, out lockAge, out lockId, out actionFlags);
        }
        
        private SessionStateStoreData GetSessionStoreItem(bool lockRecord, HttpContext context, string id, out bool locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actionFlags)
        {
            SessionStateStoreData item = null;
            lockAge = TimeSpan.Zero;
            lockId = null;
            locked = false;
            actionFlags = 0;
            DbDataReader reader = null;
            DateTime expires;
            byte[] serializedItems = null;
            bool foundRecord = false;
            bool deleteData = false;
            int timeout = 0;
            try
            {
                // Obtain a lock if possible. Ignore the record if it is expired.
                if (lockRecord)
                	using (SqlText cmd = CreateSqlText("UPDATE aspnet_sessions SET locked = @LockedTrue, lock_date = @LockDate WHERE sess" +
                            "ion_id = @SessionId AND application_name = @ApplicationName AND locked = @Locked" +
                            "False AND expires > @Expires"))
                    {
                        cmd.AddParameter("LockedTrue", 1);
                        cmd.AddParameter("LockDate", DateTime.Now);
                        cmd.AddParameter("SessionId", id);
                        cmd.AddParameter("LockedFalse", 0);
                        cmd.AddParameter("Expires", DateTime.Now);
                        if (cmd.ExecuteNonQuery() == 0)
                        {
                            // No record was updated because the record was locked or not found.
                            locked = true;
                        }
                        else
                        {
                            // The record was updated.
                            locked = false;
                        }
                    }
                // Retrieve the current session item information.
                using (SqlText cmd = CreateSqlText("SELECT expires, session_items, lock_id, lock_date, flags, timeout FROM aspnet_ses" +
                        "sions WHERE session_id = @SessionId AND application_name = @ApplicationName"))
                {
                    cmd.AddParameter("SessionId", id);
                    // Retrieve session item data from the data source.
                    reader = cmd.ExecuteReader();
                    while (cmd.Read())
                    {
                        expires = reader.GetDateTime(0);
                        if (expires < DateTime.Now)
                        {
                            // The record was expired. Mark it as not locked.
                            locked = false;
                            // The session was expired. Mark the data for deletion.
                            deleteData = true;
                        }
                        else
                        	foundRecord = true;
                        if (reader[1] == DBNull.Value)
                        	serializedItems = new byte[0];
                        else
                        	serializedItems = ((byte[])(reader[1]));
                        lockId = reader.GetInt32(2);
                        lockAge = DateTime.Now.Subtract(reader.GetDateTime(3));
                        actionFlags = ((SessionStateActions)(reader.GetInt32(4)));
                        timeout = reader.GetInt32(5);
                    }
                    reader.Close();
                }
                // If the return session item is expired, delete the record from the data source.
                if (deleteData)
                	using (SqlText cmd = CreateSqlText("DELETE FROM aspnet_sessions WHERE session_id = @SessionId AND application_name = " +
                            "@ApplicationName"))
                    {
                        cmd.AddParameter("SessionId", id);
                        cmd.ExecuteNonQuery();
                    }
                // The record was not found. Ensure that locked is false.
                if (!(foundRecord))
                	locked = false;
                // 
                //                        If the record was found and you obtain a lock, then set
                //                        the lockId, clear the actionFlags, and create the SessionStateSToreItem to return.
                //                      
                if (foundRecord && !(locked))
                {
                    lockId = (((int)(lockId)) + 1);
                    using (SqlText cmd = CreateSqlText("UPDATE aspnet_sessions SET lock_id = @LockId, flags = 0 WHERE session_id = @Sessi" +
                            "onId AND application_name = @ApplicationName"))
                    {
                        cmd.AddParameter("lockId", lockId);
                        cmd.AddParameter("SessionId", id);
                        cmd.ExecuteNonQuery();
                    }
                    // 
                    //                            If the actionFlags parameter is not InitializeItem
                    //                            deserialize the stored SessionStateItemCollection.
                    //                          
                    if (actionFlags == SessionStateActions.InitializeItem)
                    	item = CreateNewStoreData(context, Convert.ToInt32(_config.Timeout.TotalMinutes));
                    else
                    	item = Deserialize(context, serializedItems, timeout);
                }
            }
            finally
            {
                if (reader != null)
                	reader.Close();
            }
            return item;
        }
        
        private byte[] Serialize(SessionStateItemCollection items)
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(ms);
            if (items != null)
            	items.Serialize(writer);
            writer.Close();
            return ms.ToArray();
        }
        
        private SessionStateStoreData Deserialize(HttpContext context, byte[] serializedItems, int timeout)
        {
            MemoryStream ms = new MemoryStream(serializedItems);
            SessionStateItemCollection sessionItems = new SessionStateItemCollection();
            if (ms.Length > 0)
            {
                BinaryReader reader = new BinaryReader(ms);
                sessionItems = SessionStateItemCollection.Deserialize(reader);
            }
            return new SessionStateStoreData(sessionItems, SessionStateUtility.GetSessionStaticObjects(context), timeout);
        }
        
        public SqlText CreateSqlText(string sql)
        {
            SqlText cmd = new SqlText(sql, _connectionStringName);
            cmd.Command.CommandText = cmd.Command.CommandText.Replace("@", cmd.ParameterMarker);
            if (cmd.Command.CommandText.Contains((cmd.ParameterMarker + "ApplicationName")))
            	cmd.AssignParameter("ApplicationName", ApplicationName);
            cmd.Name = "MyFightTracker Session State Provider";
            cmd.WriteExceptionsToEventLog = _writeExceptionsToEventLog;
            return cmd;
        }
        
        public override void ReleaseItemExclusive(HttpContext context, string id, object lockId)
        {
            using (SqlText cmd = CreateSqlText("UPDATE aspnet_sessions SET locked = 0, expires = @Expires WHERE session_id = @Ses" +
                    "sionId AND application_name = @ApplicationName AND lock_id = @LockId"))
            {
                cmd.AddParameter("Expires", DateTime.Now.AddMinutes(_config.Timeout.TotalMinutes));
                cmd.AddParameter("SessionId", id);
                cmd.AddParameter("LockId", lockId);
                cmd.ExecuteNonQuery();
            }
        }
        
        public override void RemoveItem(HttpContext context, string id, object lockId, SessionStateStoreData item)
        {
            using (SqlText cmd = CreateSqlText("DELETE * FROM aspnet_sessions WHERE session_id = @SessionId AND application_name " +
                    "= @ApplicationName AND lock_id = @LockId"))
            {
                cmd.AddParameter("SessionId", id);
                cmd.AddParameter("LockId", lockId);
                cmd.ExecuteNonQuery();
            }
        }
        
        public override void CreateUninitializedItem(HttpContext context, string id, int timeout)
        {
            using (SqlText cmd = CreateSqlText("INSERT INTO aspnet_sessions (session_id, application_name, created, expires, lock" +
                    "_date, lock_id, timeout, locked, flags) Values(@SessionId, @ApplicationName, @Cr" +
                    "eated, @Expires, @LockDate, @LockId, @Timeout, @Locked, @Flags)"))
            {
                cmd.AddParameter("SessionId", id);
                cmd.AddParameter("Created", DateTime.Now);
                cmd.AddParameter("Expires", DateTime.Now.AddMinutes(((double)(timeout))));
                cmd.AddParameter("LockDate", DateTime.Now);
                cmd.AddParameter("LockId", 0);
                cmd.AddParameter("Timeout", timeout);
                cmd.AddParameter("Locked", 0);
                cmd.AddParameter("Flags", 1);
                cmd.ExecuteNonQuery();
            }
        }
        
        public override SessionStateStoreData CreateNewStoreData(HttpContext context, int timeout)
        {
            return new SessionStateStoreData(new SessionStateItemCollection(), SessionStateUtility.GetSessionStaticObjects(context), timeout);
        }
        
        public override void ResetItemTimeout(HttpContext context, string id)
        {
            using (SqlText cmd = CreateSqlText("UPDATE aspnet_sessions SET expires = @Expires WHERE session_id = @SessionId AND a" +
                    "pplication_name = @ApplicationName"))
            {
                cmd.AddParameter("Expires", DateTime.Now.AddMinutes(_config.Timeout.TotalMinutes));
                cmd.AddParameter("SessionId", id);
                cmd.ExecuteNonQuery();
            }
        }
        
        public override void InitializeRequest(HttpContext context)
        {
        }
        
        public override void EndRequest(HttpContext context)
        {
        }
    }
    
    public partial class PlaceholderHandler : PlaceholderHandlerBase
    {
    }
    
    public class PlaceholderHandlerBase : IHttpHandler
    {
        
        private static Regex _imageSizeRegex = new Regex("((?\'background\'[a-zA-Z0-9]+?)-((?\'textcolor\'[a-zA-Z0-9]+?)-)?)?(?\'width\'[0-9]+?)(" +
                "x(?\'height\'[0-9]*))?\\.[a-zA-Z][a-zA-Z][a-zA-Z]");
        
        bool IHttpHandler.IsReusable
        {
            get
            {
                return true;
            }
        }
        
        void IHttpHandler.ProcessRequest(HttpContext context)
        {
            // Get file name
            RouteValueDictionary routeValues = context.Request.RequestContext.RouteData.Values;
            string fileName = ((string)(routeValues["FileName"]));
            // Get extension
            string ext = Path.GetExtension(fileName);
            ImageFormat format = ImageFormat.Png;
            string contentType = "image/png";
            if (ext == ".jpg")
            {
                format = ImageFormat.Jpeg;
                contentType = "image/jpg";
            }
            else
            	if (ext == ".gif")
                {
                    format = ImageFormat.Gif;
                    contentType = "image/jpg";
                }
            // get width and height
            Match regexMatch = _imageSizeRegex.Matches(fileName)[0];
            Capture widthCapture = regexMatch.Groups["width"];
            int width = 500;
            if (widthCapture.Length != 0)
            	width = Convert.ToInt32(widthCapture.Value);
            if (width == 0)
            	width = 500;
            if (width > 4096)
            	width = 4096;
            Capture heightCapture = regexMatch.Groups["height"];
            int height = width;
            if (heightCapture.Length != 0)
            	height = Convert.ToInt32(heightCapture.Value);
            if (height == 0)
            	height = 500;
            if (height > 4096)
            	height = 4096;
            // Get background and text colors
            Color background = GetColor(regexMatch.Groups["background"], Color.LightGray);
            Color textColor = GetColor(regexMatch.Groups["textcolor"], Color.Black);
            int fontSize = ((width + height) 
                        / 50);
            if (fontSize < 10)
            	fontSize = 10;
            Font font = new Font(FontFamily.GenericSansSerif, fontSize);
            // Get text
            string text = context.Request.QueryString["text"];
            if (String.IsNullOrEmpty(text))
            	text = string.Format("{0} x {1}", width, height);
            // Get position for text
            SizeF textSize;
            using (Image img = new Bitmap(1, 1))
            {
                Graphics textDrawing = Graphics.FromImage(img);
                textSize = textDrawing.MeasureString(text, font);
            }
            // Draw the image
            using (Image image = new Bitmap(width, height))
            {
                Graphics drawing = Graphics.FromImage(image);
                drawing.Clear(background);
                using (Brush textBrush = new SolidBrush(textColor))
                	drawing.DrawString(text, font, textBrush, ((width - textSize.Width) 
                                    / 2), ((height - textSize.Height) 
                                    / 2));
                drawing.Save();
                drawing.Dispose();
                // Return image
                using (MemoryStream stream = new MemoryStream())
                {
                    image.Save(stream, format);
                    HttpCachePolicy cache = context.Response.Cache;
                    cache.SetCacheability(HttpCacheability.Public);
                    cache.SetOmitVaryStar(true);
                    cache.SetExpires(DateTime.Now.AddDays(365));
                    cache.SetValidUntilExpires(true);
                    cache.SetLastModifiedFromFileDependencies();
                    context.Response.ContentType = contentType;
                    context.Response.AddHeader("Content-Length", Convert.ToString(stream.Length));
                    context.Response.AddHeader("File-Name", fileName);
                    context.Response.BinaryWrite(stream.ToArray());
                    context.Response.OutputStream.Flush();
                }
            }
        }
        
        private static Color GetColor(Capture colorName, Color defaultColor)
        {
            try
            {
                if (colorName.Length > 0)
                {
                    string s = colorName.Value;
                    if (Regex.IsMatch(s, "^[0-9abcdef]{3,6}$"))
                    	s = ("#" + s);
                    return ColorTranslator.FromHtml(s);
                }
            }
            catch (Exception )
            {
            }
            return defaultColor;
        }
    }
    
    public class GenericRoute : IRouteHandler
    {
        
        private IHttpHandler _handler;
        
        public GenericRoute(IHttpHandler handler)
        {
            _handler = handler;
        }
        
        IHttpHandler IRouteHandler.GetHttpHandler(RequestContext context)
        {
            return _handler;
        }
        
        public static void Map(RouteCollection routes, IHttpHandler handler, string url)
        {
            Route r = new Route(url, new GenericRoute(handler));
            r.Defaults = new RouteValueDictionary();
            r.Constraints = new RouteValueDictionary();
            routes.Add(r);
        }
    }
}
