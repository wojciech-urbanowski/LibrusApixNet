namespace LibrusApixNet;

public class LibrusException(string message, Exception? innerException = null) : Exception(message, innerException);
public class TokenKeyException(string message) : LibrusException(message);
public class LibrusArgumentException(string message) : LibrusException(message);
public class TokenException(string message) : LibrusException(message);
public class AuthorizationException(string message) : LibrusException(message);
public class ParseException(string message, Exception? innerException = null) : LibrusException(message, innerException);
public class DateException(string message) : LibrusException(message);
public class MaintenanceException(string message) : LibrusException(message);
