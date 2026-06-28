using System.Security.Cryptography;
using System.Text;

namespace GSPTaskMiningAgent;

internal sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;
    public bool IsOwner { get; }
    public static string Name => $"Global\\GSPTaskMiningAgent-{Hash(Environment.UserDomainName + "\\" + Environment.UserName)}";
    public SingleInstanceGuard(){_mutex=new Mutex(true,Name,out var created);IsOwner=created;}
    public void Dispose()=>_mutex.Dispose();
    private static string Hash(string s)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant()[..16];
}
