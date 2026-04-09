---
id: versioning-docs
title: Docs Versioning Workflow
---

# Docs Versioning Workflow

This project uses Docusaurus versioned docs so contributors can keep `current` documentation accurate while maintainers preserve release-specific snapshots.

## Where docs live

Use the Docusaurus site for the main documentation experience, including:

- product and usage documentation
- contributor documentation

Keep focused repo-local markdown files when they are tied to a specific folder, template, package, or engineering workflow. `CONTRIBUTING.md` can remain as a lightweight entry point that links to the relevant docs site pages.

## Core rule

Contributors should update documentation in the same pull request as the feature or behavior change.

That means:

- Add or update the relevant page under `docs/`
- Treat `docs/` as the source of truth for the next release
- Do not create a new docs version in feature PRs

## What contributors do

When a pull request changes behavior, adds a feature, or changes recommended usage:

1. Update the relevant page in `docs/`
2. If no page exists, create one and add it to `sidebars.js`
3. Keep examples aligned with the code being merged
4. Mention the docs change in the pull request summary when useful

Contributors are writing docs for the unreleased `current` version of the product.

## Release branches and Docusaurus versions

Docusaurus versioning is file-based. It does not read release branches dynamically at runtime.

Release branches can still be the source of truth for a release line, but the published docs site is built from `master` in this repository. That means a docs snapshot created only on a release branch will not appear on the published site until the same versioned docs changes are present on `master`.

## What maintainers do at release time

The maintainer cutting the release snapshots the current docs into a new version on `master`, using the release version being prepared.

Typical release flow:

1. Ensure the release-ready docs are already merged in `docs/` on `master`
2. Create the docs snapshot for the release on `master`:

```bash
npm install
npm run version-docs -- 4.2.0
```

3. Commit and push the generated versioned docs files so the docs site can rebuild
4. Create or update the corresponding release branch for that version as needed
5. Tag and publish the GitHub release
6. Continue editing `docs/` on `master` for unreleased work after the release

The docs site build is not instantaneous, so maintainers should account for a short delay between pushing to `master` and the updated docs becoming available.

## Version naming

Use the release version you are documenting. That can be a major, minor, or patch version such as:

- `4.1.0`
- `4.2.0`
- `4.2.1`
- `5.0.0`

Keep the docs version label consistent with the release tag or package version for that snapshot.

## When to create a new docs version

Create a new version when:

- a patch release ships with docs changes worth preserving
- a new minor release ships with user-visible changes
- a new major release ships
- the release includes docs-worthy feature changes that users may need to browse by version

Usually do not create a new version for:

- every pull request
- patch releases with no meaningful docs changes
- internal-only changes with no docs impact

## Release notes vs docs

GitHub release notes and docs serve different purposes:

- Release notes summarize what changed in a release
- Docs explain how the feature works and how to use it

Release notes can point into the docs site, but they should not replace contributor-written documentation in `docs/`.

## Local docs commands

Run the docs site locally:

```bash
npm install
npm start
```

Build the static site:

```bash
npm run build
```

Create a new version snapshot:

```bash
npm run version-docs -- 4.2.0
```
