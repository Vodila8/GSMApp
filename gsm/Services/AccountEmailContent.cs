using System.Net;

namespace gsm.Services;

public static class AccountEmailContent
{
    public static string BuildAccountCreatedEmail(
        string? customerName,
        string role,
        string? customerNumber,
        string temporaryPassword,
        string confirmationUrl,
        string applicationUrl)
    {
        var name = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(customerName) ? "there" : customerName);
        var roleName = role switch
        {
            "Technician" => "Technician",
            "Administrator" => "Administrator",
            _ => "Customer"
        };
        var customerIdBlock = string.IsNullOrWhiteSpace(customerNumber)
            ? string.Empty
            : $"<div style=\"margin:0 0 20px;padding:14px 16px;background:#f1f5f9;border-radius:10px;\"><div style=\"font-size:12px;color:#64748b;text-transform:uppercase;letter-spacing:.08em;\">Customer ID</div><div style=\"margin-top:4px;font-size:20px;font-weight:700;color:#0f172a;\">{WebUtility.HtmlEncode(customerNumber)}</div></div>";
        const string serviceTerms = "<hr style=\"margin:28px 0;border:0;border-top:1px solid #e2e8f0;\"><h3 style=\"margin:0 0 10px;font-size:16px;color:#334155;\">General service terms</h3>" +
            "The service provider is not responsible for loss or damage to information, programs or data stored on the product before service." +
            "<br><br>The customer is responsible for creating a separate backup copy and removing personal information before submitting the product for service." +
            "<br><br>Warranty service does not cover products that were used or stored improperly, mechanical damage, damage caused by natural disasters, unsealed products or modules, or attempts to repair a defect by unauthorized persons." +
            "<br><br>Unclaimed products may be subject to storage or disposal fees in accordance with applicable law. By submitting a device for service, the customer agrees to the service terms and conditions." +
            "<br><br>These terms are provided in English for your reference. The applicable local legal provisions remain in force.";

        return $"<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>GSM Service Center</title></head>" +
            "<body style=\"margin:0;padding:0;background:#f1f5f9;font-family:Arial,Helvetica,sans-serif;color:#334155;line-height:1.55;\">" +
            "<div style=\"width:100%;padding:24px 10px;box-sizing:border-box;\"><div style=\"max-width:620px;margin:0 auto;background:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 4px 18px rgba(15,23,42,.10);\">" +
            "<div style=\"padding:24px 28px;background:#0d6efd;color:#ffffff;\"><div style=\"font-size:13px;letter-spacing:.08em;text-transform:uppercase;opacity:.85;\">GSM Service Center</div><h1 style=\"margin:8px 0 0;font-size:26px;line-height:1.2;\">Your account is ready</h1></div>" +
            "<div style=\"padding:28px;\"><p style=\"margin:0 0 18px;font-size:17px;\">Hello " + name + ",</p>" +
            "<p style=\"margin:0 0 20px;\">A <strong>" + roleName + "</strong> account has been created for you. Confirm your email address, then use the temporary password to sign in.</p>" +
            customerIdBlock +
            "<div style=\"margin:0 0 22px;padding:16px;background:#fff7ed;border:1px solid #fed7aa;border-radius:10px;\"><div style=\"font-size:12px;color:#9a3412;text-transform:uppercase;letter-spacing:.08em;\">Temporary password</div><div style=\"margin-top:6px;font-family:Consolas,monospace;font-size:20px;font-weight:700;letter-spacing:.04em;word-break:break-all;color:#7c2d12;\">" + WebUtility.HtmlEncode(temporaryPassword) + "</div></div>" +
            "<div style=\"text-align:center;margin:26px 0 10px;\"><a href=\"" + WebUtility.HtmlEncode(confirmationUrl) + "\" style=\"display:inline-block;box-sizing:border-box;width:100%;max-width:360px;padding:14px 20px;background:#0d6efd;color:#ffffff;text-decoration:none;border-radius:9px;font-weight:700;\">Confirm email</a></div>" +
            "<div style=\"text-align:center;margin:0 0 24px;\"><a href=\"" + WebUtility.HtmlEncode(applicationUrl) + "\" style=\"display:inline-block;box-sizing:border-box;width:100%;max-width:360px;padding:14px 20px;background:#198754;color:#ffffff;text-decoration:none;border-radius:9px;font-weight:700;\">Open application</a></div>" +
            "<p style=\"margin:0;font-size:13px;color:#64748b;\">If you did not expect this account, you can ignore this email.</p>" + serviceTerms +
            "</div></div><div style=\"max-width:620px;margin:14px auto 0;text-align:center;font-size:12px;color:#94a3b8;\">This is an automatic notification from GSM Service Center.</div></div></body></html>";
    }
}
