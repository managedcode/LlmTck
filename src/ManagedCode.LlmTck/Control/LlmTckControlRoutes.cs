namespace ManagedCode.LlmTck.Control;

public static class LlmTckControlRoutes
{
    public const string Admin = "/admin/llm-tck";
    public const string Models = Admin + "/models";
    public const string Assertions = Admin + "/assertions";
    public const string Configure = Admin + "/configure";
    public const string Reset = Admin + "/reset";
}
