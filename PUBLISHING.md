# Publish Project Kundial on GitHub Pages

The website is in `docs/` and uses plain HTML, CSS, and JavaScript.

1. [Create a GitHub repository](https://github.com/new) named `project-kundial`. Choose your account as owner. GitHub Free serves Pages from public repositories, so choose **Public** when you're ready to share the site and source.
2. Upload the contents of the extracted `ProjectKundial` folder into the repository so `docs/`, `Source/`, and `GameData/` sit at its top level. You can use **Add file → Upload files** in an initialized repository or push with Git.
3. Open **Settings → Pages**. Under **Build and deployment**, set **Source** to **Deploy from a branch**. Select **main** and **/docs**, then click **Save**.
4. Once it deploys, open the URL shown under **Settings → Pages**. For a repository named `project-kundial`, it will normally be `https://YOUR-USERNAME.github.io/project-kundial/`.

[GitHub Pages publishing documentation](https://docs.github.com/en/pages/getting-started-with-github-pages/configuring-a-publishing-source-for-your-github-pages-site)

## Update the source download

The site's download is a versioned snapshot in `docs/downloads/`. When releasing a new version, package `Source/`, the tracked files in `GameData/`, `README.md`, `PUBLISHING.md`, `build.sh` and `build-windows.cmd` at the root of the ZIP. Exclude compiled DLLs and KSP assemblies. Update the download filename and version shown in `docs/index.html`.
