namespace Charlotte.Windows.Assets;

public sealed record AssetIssue(string Code, string Message, string Severity = "error");
