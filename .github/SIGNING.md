# Code signing

## Why this exists

CyberWall ships an unsigned installer. Unsigned, compressed installers are a classic false positive pattern for antivirus engines: with `v2.3.0`, Microsoft flagged the setup with `Trojan:Win32/Wacatac.B!ml` and Kaspersky with `HEUR:Trojan.Win32.Sdum.gen`, even though the application executable and the portable archive were clean and the installer matched the SHA256 published by CI. SmartScreen also blocks the download until the file earns reputation.

Signing removes the "anonymous binary" from the equation and is the only durable fix. Instead of buying a certificate, CyberWall targets [SignPath Foundation](https://signpath.org/), which issues a free code signing certificate to open source projects and signs builds from GitHub Actions.

> **Status: not configured yet.** Every release published so far is unsigned. This document describes the setup that has to be completed before the signing steps are added to `.github/workflows/release.yml`.

## Signing order

The order matters because the Inno Setup installer embeds the published files:

1. Build and publish the application.
2. Sign `CyberWall.exe` and `CyberWall.Service.exe`.
3. Compile the installer from the signed files.
4. Sign the installer.
5. Publish the signed installer and the portable archive built from the signed files.

Signing the installer after compiling it, without signing its payload first, leaves an unsigned application inside a signed container.

## Onboarding checklist

1. Apply at <https://signpath.org/apply> with the repository URL, the license (GPLv3), and a maintainer contact. Approval is manual and can take several days.
2. Install the [SignPath GitHub App](https://github.com/apps/signpath) and grant it access to `CyberGems/CyberWall`.
3. In SignPath, link the predefined *GitHub.com* trusted build system to the project.
4. Create the two artifact configurations described below, plus a signing policy for releases.
5. Collect the identifiers and store them in the repository:
   - Secret `SIGNPATH_API_TOKEN`: API token of a SignPath user with submitter permissions.
   - Variable `SIGNPATH_ORGANIZATION_ID`: organization ID.
   - Variable `SIGNPATH_PROJECT_SLUG`: project slug.
   - Variable `SIGNPATH_SIGNING_POLICY_SLUG`: signing policy slug.

SignPath Foundation requires the workflow to run on GitHub-hosted runners, and the artifact has to be uploaded to GitHub before the signing request is submitted.

## Artifact configurations

Both artifacts are uploaded with `actions/upload-artifact`, so the root element is `<zip-file>`.

`cyberwall-binaries`:

```xml
<artifact-configuration xmlns="http://signpath.io/artifact-configuration">
  <zip-file>
    <pe-file path="CyberWall.exe" />
    <pe-file path="CyberWall.Service.exe" />
  </zip-file>
</artifact-configuration>
```

`cyberwall-installer`:

```xml
<artifact-configuration xmlns="http://signpath.io/artifact-configuration">
  <zip-file>
    <pe-file path="CyberWall-Setup-*.exe" />
  </zip-file>
</artifact-configuration>
```

## Workflow steps to enable

These steps are **not** in `release.yml` yet. They are the shape the release pipeline takes once the identifiers above exist; add them in the order shown, between the existing publish and installer steps.

Sign the application payload before compiling the installer:

```yaml
      - name: Upload unsigned application binaries
        id: upload-unsigned-binaries
        uses: actions/upload-artifact@v4
        with:
          name: cyberwall-binaries
          path: |
            publish-win64/CyberWall.exe
            publish-win64/CyberWall.Service.exe
          if-no-files-found: error

      - name: Sign application binaries
        uses: signpath/github-action-submit-signing-request@v3
        with:
          api-token: ${{ secrets.SIGNPATH_API_TOKEN }}
          organization-id: ${{ vars.SIGNPATH_ORGANIZATION_ID }}
          project-slug: ${{ vars.SIGNPATH_PROJECT_SLUG }}
          signing-policy-slug: ${{ vars.SIGNPATH_SIGNING_POLICY_SLUG }}
          artifact-configuration-slug: cyberwall-binaries
          github-artifact-id: ${{ steps.upload-unsigned-binaries.outputs.artifact-id }}
          wait-for-completion: true
          output-artifact-directory: ./signed-binaries
```

Copy `./signed-binaries` over `./publish-win64` before the portable archive and the installer are produced. Then sign the compiled installer:

```yaml
      - name: Upload unsigned installer
        id: upload-unsigned-installer
        uses: actions/upload-artifact@v4
        with:
          name: cyberwall-installer
          path: CyberWall-Setup-${{ github.ref_name }}.exe
          if-no-files-found: error

      - name: Sign installer
        uses: signpath/github-action-submit-signing-request@v3
        with:
          api-token: ${{ secrets.SIGNPATH_API_TOKEN }}
          organization-id: ${{ vars.SIGNPATH_ORGANIZATION_ID }}
          project-slug: ${{ vars.SIGNPATH_PROJECT_SLUG }}
          signing-policy-slug: ${{ vars.SIGNPATH_SIGNING_POLICY_SLUG }}
          artifact-configuration-slug: cyberwall-installer
          github-artifact-id: ${{ steps.upload-unsigned-installer.outputs.artifact-id }}
          wait-for-completion: true
          output-artifact-directory: ./signed-installer
```

Replace the unsigned installer with the signed one before the release is created, and let the SHA256 in the release notes be computed from the signed file, not from the unsigned one.

## Verification

```powershell
Get-AuthenticodeSignature .\CyberWall-Setup-vX.Y.Z.exe | Format-List Status, SignerCertificate
```

A signed build reports `Status: Valid` and a timestamp counter-signature. The release notes should keep publishing the SHA256 of the signed files so users can verify the download independently.

## Until signing is enabled

- Keep publishing the SHA256 of every artifact in the release notes.
- Report false positives to Microsoft (<https://www.microsoft.com/en-us/wdsi/filesubmission>) and Kaspersky (<https://opentip.kaspersky.com/>), and request a reanalysis on VirusTotal once the vendors reply.
- The README and the wiki explain how to keep a blocked download and how to allow the file from Windows Security.

If SignPath Foundation declines the project, the fallback is a Certum Open Source Code Signing certificate used with `signtool` in the same two stages.
