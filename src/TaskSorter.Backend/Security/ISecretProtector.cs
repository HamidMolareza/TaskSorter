namespace TaskSorter.Backend.Security;

public interface ISecretProtector
{
    string Protect(string value);
    string Unprotect(string protectedValue);
}
