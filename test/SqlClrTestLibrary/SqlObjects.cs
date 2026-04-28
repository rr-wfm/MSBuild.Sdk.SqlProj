using Microsoft.SqlServer.Server;

namespace SqlClrTestLibrary
{
    public static class SqlObjects
    {
        [SqlFunction]
        public static int ReturnOne()
        {
            return 1;
        }
    }
}
