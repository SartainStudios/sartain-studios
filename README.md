# **Sartain Studios LLC - System Design**

## **System Overview**

The platform serves a dual purpose:

1. **Public Portfolio & Services:** Request custom app and/or website development.
2. **SaaS Platforms:**
    1. **Invoicing:** A fully-featured invoicing and time-tracking system.

## **Technology Stack**

- **Frontend:** Blazor WebAssembly (.NET 10), hosted on GitHub Pages.
- **Backend:** ASP.NET Core Web API (.NET 10), hosted on MonsterASP.NET.
- **Database:** MongoDB hosted on MongoDB Cloud.
- **Authentication:** ASP.NET Core Identity. Users can log in using Google OAuth, Email/Password, or both.
- **PDF Generation:** QuestPDF for generating invoice PDFs.
- **Email Service:** SMTP over TLS for sending emails.
- **Analytics:** Google Analytics for usage and traffic insights on the Blazor WebAssembly frontend.
- **CI/CD:** GitHub Actions for automated build, test, and deployment pipelines.~~~~