const config = {
  title: "MSBuild.Sdk.SqlProj",
  tagline: "Build SQL Server dacpacs with SDK-style .NET projects",
  url: "https://rr-wfm.github.io",
  baseUrl: "/MSBuild.Sdk.SqlProj/",
  organizationName: "rr-wfm",
  projectName: "MSBuild.Sdk.SqlProj",
  onBrokenLinks: "throw",
  i18n: {
    defaultLocale: "en",
    locales: ["en"]
  },
  markdown: {
    mermaid: true,
    hooks: {
      onBrokenMarkdownLinks: "warn"
    }
  },
  presets: [
    [
      "classic",
      {
        docs: {
          routeBasePath: "/",
          sidebarPath: require.resolve("./sidebars.js"),
          editUrl: "https://github.com/rr-wfm/MSBuild.Sdk.SqlProj/edit/master/"
        },
        blog: false,
        pages: false,
        theme: {
          customCss: require.resolve("./src/css/custom.css")
        }
      }
    ]
  ],
  themeConfig: {
    colorMode: {
      defaultMode: "dark",
      disableSwitch: false,
      respectPrefersColorScheme: true
    },
    navbar: {
      title: "MSBuild.Sdk.SqlProj",
      items: [
        {
          type: "docSidebar",
          sidebarId: "docsSidebar",
          position: "left",
          label: "Documentation"
        },
        {
          type: "docsVersionDropdown",
          position: "right",
          dropdownActiveClassDisabled: true
        },
        {
          href: "https://github.com/rr-wfm/MSBuild.Sdk.SqlProj",
          label: "GitHub",
          position: "right"
        },
        {
          href: "https://www.nuget.org/packages/MSBuild.Sdk.SqlProj",
          label: "NuGet",
          position: "right"
        }
      ]
    },
    footer: {
      style: "dark",
      links: [
        {
          title: "Docs",
          items: [
            {
              label: "Getting Started",
              to: "/getting-started"
            },
            {
              label: "Versioning Workflow",
              to: "/versioning-docs"
            }
          ]
        },
        {
          title: "Project",
          items: [
            {
              label: "Repository",
              href: "https://github.com/rr-wfm/MSBuild.Sdk.SqlProj"
            },
            {
              label: "Releases",
              href: "https://github.com/rr-wfm/MSBuild.Sdk.SqlProj/releases"
            }
          ]
        }
      ]
    },
    prism: {
      additionalLanguages: ["powershell", "csharp", "sql"]
    },
    mermaid: {
      theme: {
        light: "neutral",
        dark: "dark"
      }
    }
  },
  themes: ["@docusaurus/theme-mermaid"]
};

module.exports = config;
