# Privacy Policy - Multiplatform Native WebView (Unity Asset)

**Last updated:** May 12, 2026

This Privacy Policy describes how the **Multiplatform Native WebView** Unity asset (the "Asset") handles data.

For questions about this policy, contact the Publisher at: **[ADD CONTACT EMAIL OR URL]**.

## 1. Scope

This policy applies to the Asset package distributed on the Unity Asset Store, including its runtime code for Android, iOS, and WebGL.

This policy does **not** replace the privacy policy of the game/app/website that uses the Asset. If you use this Asset in your product, you are responsible for your own privacy disclosures and legal compliance.

## 2. Data Collection by the Asset Publisher

The Asset itself is designed to work locally in your Unity project and does not include a Publisher-hosted backend service that receives end-user browsing data by default.

The Publisher does not intentionally collect, store, or sell end-user personal data through the Asset runtime itself.

## 3. Data Processed on End-User Devices

When integrated into an app/game, the Asset can display web content via platform webviews/iframe. Depending on how you configure it, the Asset may process:

- URLs provided by the host app.
- HTML strings provided by the host app.
- Runtime UI configuration values (toolbar settings, paddings, colors, icon choices).
- Local temporary data required by platform webview components.

On native platforms, your app may save local HTML to app storage when using file-based loading modes.

## 4. Third-Party Websites and Services

Any website loaded through the Asset is controlled by that website operator, not by the Asset Publisher.

Those websites may independently collect personal data (for example via cookies, authentication, analytics, ads, or tracking technologies) according to their own policies.

Some websites block embedding in webviews/iframes via security headers (for example CSP `frame-ancestors` or `X-Frame-Options`). In such cases, the content may open in a browser tab/window or fail to render in-frame.

## 5. WebGL-Specific Notes

For WebGL builds:

- Content is rendered using browser iframe/new-tab mechanisms.
- Browser security policies apply (CSP, same-origin rules, popup policies).
- Local device-style file paths are not a reliable cross-browser content source; HTTP/HTTPS or provided HTML content should be used.

## 6. Data Sharing and Sale

The Asset runtime does not include functionality to sell personal information.

Data sharing can occur only through:

- Websites/services chosen by the integrator to load in webview.
- Platform/browser components required to render those sites.

## 7. Data Retention

The Asset does not maintain a Publisher cloud database for runtime browsing data.

Any retention depends on:

- The host app's own storage behavior.
- OS/browser webview cache/cookie/session behavior.
- Third-party websites loaded by the host app.

## 8. Security

The Asset relies on platform security features provided by iOS, Android, and modern browsers.

Integrators should:

- Prefer HTTPS URLs.
- Load only trusted domains.
- Provide clear user disclosure when loading external web content.
- Review platform permissions and webview settings for least privilege.

## 9. Children

The Asset is a developer tool and is not directed to children as an end-user product.

If your app targets children, you are responsible for implementing child-appropriate privacy protections and disclosures.

## 10. International Transfers

The Asset Publisher does not operate a default runtime data transfer backend for this Asset.

However, websites loaded through the Asset may transfer data internationally under their own policies.

## 11. Your Responsibilities as Integrator

If you publish an app/game using this Asset, you should provide an end-user privacy policy that clearly explains:

- Which websites/services are loaded.
- What user data your app collects.
- Whether cookies/tracking/auth are used.
- How users can contact you and exercise applicable privacy rights.

## 12. Changes to This Policy

This policy may be updated from time to time. Material updates should include a revised "Last updated" date.

## 13. Contact

Publisher contact: **[ADD CONTACT EMAIL OR URL]**

Business name (optional): **[ADD LEGAL ENTITY NAME]**

---

**Important:** This template is for informational purposes and is not legal advice. Consult qualified counsel to adapt this policy to your jurisdiction and product requirements.
