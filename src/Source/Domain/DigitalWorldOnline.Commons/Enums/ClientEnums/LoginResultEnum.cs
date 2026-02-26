namespace DigitalWorldOnline.Commons.Enums.ClientEnums
{
    public enum LoginResultEnum
    {
        Success = 0,
        /// <summary>
        /// Username not found on database
        /// </summary>
        UserNotFound = 18,

        /// <summary>
        /// Account has been banned
        /// </summary>
        BannedAccount = 68,

        /// <summary>
        /// Wrong password for this account
        /// </summary>
        IncorrectPassword = 73,

        SERVER_IS_MAINTENANCE = 10011,
        SERVER_CONNECT_USER_FULL = 10012,
        ALREADY_LOGIN = 10014,
        SERVER_IS_NOT_READY = 10015,
        AUTH_FAILED = 10035,
        ACCOUNT_BANNED = 10052,
        ERROR_LOGINPASS = 10057,
        ERROR_ID = 10058


    }
}
