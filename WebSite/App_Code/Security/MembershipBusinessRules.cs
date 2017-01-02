using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Mail;
using System.Web;
using System.Web.Security;
using MyFightTracker.Data;
using MyFightTracker.Services;

namespace MyFightTracker.Security
{
	public partial class MembershipBusinessRules : MembershipBusinessRulesBase
    {
    }
    
    public class MembershipBusinessRulesBase : BusinessRules
    {
        
        public static SortedDictionary<MembershipCreateStatus, string> CreateErrors = new SortedDictionary<MembershipCreateStatus, string>();
        
        static MembershipBusinessRulesBase()
        {
            CreateErrors.Add(MembershipCreateStatus.DuplicateEmail, "Duplicate email address.");
            CreateErrors.Add(MembershipCreateStatus.DuplicateProviderUserKey, "Duplicate provider key.");
            CreateErrors.Add(MembershipCreateStatus.DuplicateUserName, "Duplicate user name.");
            CreateErrors.Add(MembershipCreateStatus.InvalidAnswer, "Invalid password recovery answer.");
            CreateErrors.Add(MembershipCreateStatus.InvalidEmail, "Invalid email address.");
            CreateErrors.Add(MembershipCreateStatus.InvalidPassword, "Invalid password.");
            CreateErrors.Add(MembershipCreateStatus.InvalidProviderUserKey, "Invalid provider user key.");
            CreateErrors.Add(MembershipCreateStatus.InvalidQuestion, "Invalid password recovery question.");
            CreateErrors.Add(MembershipCreateStatus.InvalidUserName, "Invalid user name.");
            CreateErrors.Add(MembershipCreateStatus.ProviderError, "Provider error.");
            CreateErrors.Add(MembershipCreateStatus.UserRejected, "User has been rejected.");
        }
        
        [RowFilter("aspnet_Membership", "signUpForm", "UserName")]
        [RowFilter("aspnet_Membership", "passwordRequestForm", "UserName")]
        protected virtual string FakeUserName
        {
            get
            {
                // We will filter user rows by a fake name to prevent 
                // return of any existing user records through sign-up
                // or password recovery forms
                return "__No_Name";
            }
        }
        
        [RowFilter("aspnet_Membership", "identityConfirmationForm", "UserName")]
        protected virtual string IdentityConfirmationUserName
        {
            get
            {
                // We are filtering rows by a user name provided to PasswordRequest method
                return ((string)(Context.Session["IdentityConfirmation"]));
            }
        }
        
        [RowFilter("aspnet_Membership", "myAccountForm", "UserName")]
        protected virtual string CurrentUserName
        {
            get
            {
                return Context.User.Identity.Name;
            }
        }
        
        [ControllerAction("aspnet_Membership", "Delete", ActionPhase.Before)]
        protected virtual void DeleteUser(int userId)
        {
            PreventDefault();
            MembershipUser user = Membership.GetUser(userId);
            Membership.DeleteUser(user.UserName);
            Result.ShowLastView();
            Result.ShowMessage(String.Format(Localize("UserHasBeenDeleted", "User \'{0}\' has been deleted."), user.UserName));
        }
        
        [ControllerAction("aspnet_Membership", "Update", ActionPhase.Before)]
        protected virtual void UpdateUser(int userId, FieldValue email, FieldValue isApproved, FieldValue isLockedOut, FieldValue comment, FieldValue roles)
        {
            PreventDefault();
            MembershipUser user = Membership.GetUser(userId);
            // update user information
            if (email.Modified)
            {
                user.Email = Convert.ToString(email.Value);
                Membership.UpdateUser(user);
            }
            if (isApproved.Modified)
            {
                user.IsApproved = Convert.ToBoolean(isApproved.Value);
                Membership.UpdateUser(user);
            }
            if (isLockedOut.Modified)
            {
                if (Convert.ToBoolean(isLockedOut.Value))
                {
                    Result.Focus("IsLockedOut", Localize("UserCannotBeLockedOut", "User cannot be locked out. If you want to prevent this user from being able to lo" +
                                "gin then simply mark user as \'not-approved\'."));
                    throw new Exception(Localize("ErrorSavingUser", "Error saving user account."));
                }
                user.UnlockUser();
            }
            if (comment.Modified)
            {
                user.Comment = Convert.ToString(comment.Value);
                Membership.UpdateUser(user);
            }
            if (roles.Modified)
            {
                string[] newRoles = Convert.ToString(roles.Value).Split(',');
                string[] oldRoles = System.Web.Security.Roles.GetRolesForUser(user.UserName);
                foreach (string role in oldRoles)
                	if (!(String.IsNullOrEmpty(role)) && (Array.IndexOf(newRoles, role) == -1))
                    	System.Web.Security.Roles.RemoveUserFromRole(user.UserName, role);
                foreach (string role in newRoles)
                	if (!(String.IsNullOrEmpty(role)) && (Array.IndexOf(oldRoles, role) == -1))
                    	System.Web.Security.Roles.AddUserToRole(user.UserName, role);
            }
        }
        
        [ControllerAction("aspnet_Membership", "Insert", ActionPhase.Before)]
        protected virtual void InsertUser(string username, string password, string confirmPassword, string email, string passwordQuestion, string passwordAnswer, bool isApproved, string comment, string roles)
        {
            PreventDefault();
            if (password != confirmPassword)
            	throw new Exception(Localize("PasswordAndConfirmationDoNotMatch", "Password and confirmation do not match."));
            // create a user
            MembershipCreateStatus status;
            Membership.CreateUser(username, password, email, passwordQuestion, passwordAnswer, isApproved, out status);
            if (status != MembershipCreateStatus.Success)
            	throw new Exception(Localize(status.ToString(), MembershipBusinessRules.CreateErrors[status]));
            // retrieve the primary key of the new user account
            MembershipUser newUser = Membership.GetUser(username);
            object providerUserKey = newUser.ProviderUserKey;
            if (providerUserKey.GetType() == typeof(byte[]))
            	providerUserKey = new Guid(((byte[])(providerUserKey)));
            Result.Values.Add(new FieldValue("UserId", providerUserKey));
            // update a comment
            if (!(String.IsNullOrEmpty(comment)))
            {
                newUser.Comment = comment;
                Membership.UpdateUser(newUser);
            }
            if (!(String.IsNullOrEmpty(roles)))
            	foreach (string role in roles.Split(','))
                	System.Web.Security.Roles.AddUserToRole(username, role);
        }
        
        [ControllerAction("aspnet_Membership", "signUpForm", "Insert", ActionPhase.Before)]
        protected virtual void SignUpUser(string username, string password, string confirmPassword, string email, string passwordQuestion, string passwordAnswer)
        {
            InsertUser(username, password, confirmPassword, email, passwordQuestion, passwordAnswer, true, Localize("SelfRegisteredUser", "Self-registered user."), "Users");
        }
        
        [RowBuilder("aspnet_Membership", "createForm1", RowKind.New)]
        protected virtual void NewUserRow()
        {
            UpdateFieldValue("UserName", "user name");
            UpdateFieldValue("IsApproved", true);
        }
        
        [RowBuilder("aspnet_Membership", "editForm1", RowKind.Existing)]
        protected virtual void PrepareUserRow()
        {
            string userName = ((string)(SelectFieldValue("UserUserName")));
            StringBuilder sb = new StringBuilder();
            foreach (string role in System.Web.Security.Roles.GetRolesForUser(userName))
            {
                if (sb.Length > 0)
                	sb.Append(',');
                sb.Append(role);
            }
            UpdateFieldValue("Roles", sb.ToString());
            DateTime dt = ((DateTime)(SelectFieldValue("LastLockoutDate")));
            if (dt.Equals(new DateTime(1754, 1, 1)))
            	UpdateFieldValue("LastLockoutDate", null);
            dt = ((DateTime)(SelectFieldValue("FailedPasswordAttemptWindowStart")));
            if (dt.Equals(new DateTime(1754, 1, 1)))
            	UpdateFieldValue("FailedPasswordAttemptWindowStart", null);
            dt = ((DateTime)(SelectFieldValue("FailedPasswordAnswerAttemptWindowStart")));
            if (dt.Equals(new DateTime(1754, 1, 1)))
            	UpdateFieldValue("FailedPasswordAnswerAttemptWindowStart", null);
        }
        
        [ControllerAction("aspnet_Roles", "Insert", ActionPhase.Before)]
        protected virtual void InsertRole(string roleName)
        {
            PreventDefault();
            System.Web.Security.Roles.CreateRole(roleName);
        }
        
        [ControllerAction("aspnet_Roles", "Delete", ActionPhase.Before)]
        protected virtual void DeleteRole(string roleName)
        {
            PreventDefault();
            System.Web.Security.Roles.DeleteRole(roleName);
        }
        
        [RowBuilder("aspnet_Membership", "passwordRequestForm", RowKind.New)]
        protected virtual void NewPasswordRequestRow()
        {
            UpdateFieldValue("UserName", Context.Session["IdentityConfirmation"]);
        }
        
        [ControllerAction("aspnet_Membership", "passwordRequestForm", "Custom", "RequestPassword", ActionPhase.Execute)]
        protected virtual void PasswordRequest(string userName)
        {
            PreventDefault();
            MembershipUser user = Membership.GetUser(userName);
            if (user == null)
            	Result.ShowAlert(Localize("UserNameDoesNotExist", "User name does not exist."), "UserName");
            else
            {
                Context.Session["IdentityConfirmation"] = userName;
                Result.HideModal();
                Result.ShowModal("aspnet_Membership", "identityConfirmationForm", "Edit", "identityConfirmationForm");
            }
        }
        
        [RowBuilder("aspnet_Membership", "identityConfirmationForm", RowKind.Existing)]
        protected virtual void PrepareIdentityConfirmationRow()
        {
            UpdateFieldValue("PasswordAnswer", null);
        }
        
        [ControllerAction("aspnet_Membership", "identityConfirmationForm", "Custom", "ConfirmIdentity", ActionPhase.Execute)]
        protected virtual void IdentityConfirmation(string userName, string passwordAnswer)
        {
            PreventDefault();
            MembershipUser user = Membership.GetUser(userName);
            if (user != null)
            {
                string newPassword = user.ResetPassword(passwordAnswer);
                // create an email and send it to the user
                MailMessage message = new MailMessage();
                message.To.Add(user.Email);
                message.Subject = String.Format(Localize("NewPasswordSubject", "New password for \'{0}\'."), userName);
                message.Body = newPassword;
                SmtpClient client = new SmtpClient();
                client.Send(message);
                // hide modal popup and display a confirmation
                Result.HideModal();
                Result.ShowAlert(Localize("NewPasswordAlert", "A new password has been emailed to the address on file."));
            }
        }
        
        [RowBuilder("aspnet_Membership", "myAccountForm", RowKind.Existing)]
        protected virtual void PrepareCurrentUserRow()
        {
            UpdateFieldValue("OldPassword", null);
            UpdateFieldValue("Password", null);
            UpdateFieldValue("ConfirmPassword", null);
            UpdateFieldValue("PasswordAnswer", null);
        }
        
        [ControllerAction("aspnet_Membership", "identityConfirmationForm", "Custom", "BackToRequestPassword", ActionPhase.Execute)]
        protected virtual void BackToRequestPassword()
        {
            PreventDefault();
            Result.HideModal();
            Result.ShowModal("aspnet_Membership", "passwordRequestForm", "New", "passwordRequestForm");
        }
        
        [ControllerAction("aspnet_Membership", "myAccountForm", "Update", ActionPhase.Before)]
        protected virtual void UpdateMyAccount(string userName, string oldPassword, string password, string confirmPassword, string email, string passwordQuestion, string passwordAnswer)
        {
            PreventDefault();
            MembershipUser user = Membership.GetUser(userName);
            if (user != null)
            {
                if (String.IsNullOrEmpty(oldPassword))
                {
                    Result.ShowAlert(Localize("EnterCurrentPassword", "Please enter your current password."), "OldPassword");
                    return;
                }
                if (!(Membership.ValidateUser(userName, oldPassword)))
                {
                    Result.ShowAlert(Localize("PasswordDoesNotMatchRecords", "Your password does not match our records."), "OldPassword");
                    return;
                }
                if (!(String.IsNullOrEmpty(password)) || !(String.IsNullOrEmpty(confirmPassword)))
                {
                    if (password != confirmPassword)
                    {
                        Result.ShowAlert(Localize("NewPasswordAndConfirmatinDoNotMatch", "New password and confirmation do not match."), "Password");
                        return;
                    }
                    if (!(user.ChangePassword(oldPassword, password)))
                    {
                        Result.ShowAlert(Localize("NewPasswordInvalid", "Your new password is invalid."), "Password");
                        return;
                    }
                }
                if (email != user.Email)
                {
                    user.Email = email;
                    Membership.UpdateUser(user);
                }
                if (user.PasswordQuestion != passwordQuestion && String.IsNullOrEmpty(passwordAnswer))
                {
                    Result.ShowAlert(Localize("EnterPasswordAnswer", "Please enter a password answer."), "PasswordAnswer");
                    return;
                }
                if (!(String.IsNullOrEmpty(passwordAnswer)))
                {
                    user.ChangePasswordQuestionAndAnswer(oldPassword, passwordQuestion, passwordAnswer);
                    Membership.UpdateUser(user);
                }
                Result.HideModal();
            }
            else
            	Result.ShowAlert(Localize("UserNotFound", "User not found."));
        }
        
        public static void CreateStandardMembershipAccounts()
        {
            ApplicationServices.RegisterStandardMembershipAccounts();
        }
        
        [ControllerAction("aspnet_Membership", "Select", ActionPhase.Before)]
        public virtual void AccessControlValidation()
        {
            if (Context.User.Identity.IsAuthenticated)
            	return;
            if (!((((Request.View == "signUpForm") || (Request.View == "passwordRequestForm")) || ((Request.View == "identityConfirmationForm") || (Request.View == "loginForm")))))
            	throw new Exception("Not authorized");
        }
    }
}
