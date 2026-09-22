namespace gsm.Services;

public record EmailMessage(string To, string Subject, string HtmlBody);
