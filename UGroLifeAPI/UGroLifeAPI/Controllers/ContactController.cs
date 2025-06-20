using Microsoft.AspNetCore.Mvc;
using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using System;
using System.Threading.Tasks;

// Assuming ContactFormModel is defined somewhere like this:
public class ContactFormModel
{
    public string Name { get; set; }
    public string Email { get; set; }
    public string Message { get; set; }
    public string Mobile { get; set; }
}

[ApiController]
[Route("api/[controller]")]
public class ContactController : ControllerBase
{
    // These should ideally be loaded from appsettings.json in a real application
    private readonly string _sendingGmailAddress = "lineandleaf25@gmail.com"; // Your Gmail address
    private readonly string _gmailAppPassword = "yrcy krxb izun whhp"; // Your App Password
    private readonly string _recipientEmail = "lineandleaf25@gmail.com"; // Where contact forms go

    [HttpPost("send-email")]
    public async Task<IActionResult> SendEmail([FromBody] ContactFormModel model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        bool primaryEmailSent = false;
        try
        {
            // 1. Send the primary contact message to your recipient
            var messageToRecipient = new MimeMessage();
            messageToRecipient.From.Add(new MailboxAddress("UGroLife Contact Form", _sendingGmailAddress));
            messageToRecipient.To.Add(new MailboxAddress("UGroLife Recipient", _recipientEmail));
            messageToRecipient.Subject = $"New Contact Message from UGroLife: {model.Name}";

            var bodyBuilderToRecipient = new BodyBuilder();
            bodyBuilderToRecipient.HtmlBody = $@"
                <p><strong>Name:</strong> {model.Name}</p>
                <p><strong>Email:</strong> {model.Email}</p>
                <p><strong>Message:</strong></p>
                <p>{model.Message}</p>
                <hr>
                <p><em>This message was sent via the UGroLife website contact form.</em></p>
            ";
            messageToRecipient.Body = bodyBuilderToRecipient.ToMessageBody();

            using (var client = new SmtpClient())
            {
                await client.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(_sendingGmailAddress, _gmailAppPassword);
                await client.SendAsync(messageToRecipient);
                await client.DisconnectAsync(true);
            }

            primaryEmailSent = true;

            try
            {
                var autoReplyMessage = new MimeMessage();
                autoReplyMessage.From.Add(new MailboxAddress("UGroLife Support", _sendingGmailAddress)); 
                autoReplyMessage.To.Add(new MailboxAddress(model.Name, model.Email)); 
                autoReplyMessage.Subject = "Thank You for Contacting UGroLife!";

                var autoReplyBodyBuilder = new BodyBuilder();
                autoReplyBodyBuilder.HtmlBody = $@"
                    <p>Dear {model.Name},</p>
                    <p>Thank you for reaching out to UGroLife! We have successfully received your message and appreciate you taking the time to contact us.</p>
                    <p>We will review your inquiry and get back to you as soon as possible, typically within 24-48 business hours.</p>
                    <p>In the meantime, feel free to explore our website or check out our FAQs if you have immediate questions.</p>
                    <p>We look forward to connecting with you!</p>
                    <p>Best regards,<br>The UGroLife Team</p>
                    <p><img src=""https://placehold.co/100x100/4CAF50/FFFFFF?text=🌱"" alt=""UGroLife Logo"" style=""border-radius: 50%;""></p>
                ";
                autoReplyMessage.Body = autoReplyBodyBuilder.ToMessageBody();

                using (var client = new SmtpClient())
                {
                    await client.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
                    await client.AuthenticateAsync(_sendingGmailAddress, _gmailAppPassword);
                    await client.SendAsync(autoReplyMessage);
                    await client.DisconnectAsync(true);
                }
                Console.WriteLine($"Successfully sent auto-reply to {model.Email}");
            }
            catch (Exception autoReplyEx)
            {
                // Log the auto-reply error, but don't fail the primary contact form submission
                Console.WriteLine($"Error sending auto-reply to {model.Email}: {autoReplyEx.Message}");
                // You might want to store this in a database to retry later or notify an admin
            }

            return Ok(new { message = "Email sent successfully! We have also sent a confirmation to your email." });
        }
        catch (MailKit.Security.AuthenticationException authEx)
        {
            Console.WriteLine($"MailKit Authentication Error: {authEx.Message}. Check your email and App Password.");
            return StatusCode(500, new { message = "Failed to send email. Authentication failed. Check your email and App Password." });
        }
        catch (MailKit.Net.Smtp.SmtpCommandException smtpCmdEx)
        {
            Console.WriteLine($"MailKit SMTP Command Error: {smtpCmdEx.ErrorCode} - {smtpCmdEx.Message}");
            return StatusCode(500, new { message = $"Failed to send email due to SMTP command error: {smtpCmdEx.ErrorCode}." });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"General Error sending email: {ex.Message}");
            string baseErrorMessage = "Failed to send email. Please try again later.";
            if (!primaryEmailSent)
            {
                // If the primary email failed, the error message should reflect that.
                return StatusCode(500, new { message = baseErrorMessage });
            }
            else
            {
                // If only the auto-reply failed, we might still return success for the contact form,
                // but inform about the auto-reply issue if needed (or just log it).
                // For simplicity, we'll still report a general error if any part of the process fails.
                return StatusCode(500, new { message = baseErrorMessage + " (There was also an issue sending the auto-reply.)" });
            }
        }
    }
}