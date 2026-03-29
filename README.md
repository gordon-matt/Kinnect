# Kinnect

**A self-hosted family tree and private social space for people who want to stay in touch without the noise.**

---

## Why this exists

This project exists to fill a gap. There are plenty of genealogical apps out there—even self-hosted ones like [webtrees](https://www.webtrees.net/). Those tools tend to be aimed at serious genealogical research. Most people I know would be happy with a family tree they can actually navigate, and a straightforward way to stay in touch.

I have come to dislike large social platforms: more ads than conversation, and mechanics like likes and dislikes that feel needlessly toxic. Kinnect is built for family members who want to share photos and videos, view a family tree, and talk to each other—on infrastructure you control.

[SCREENSHOT]

---

## Features

### Interactive family tree

Browse your family as an interactive chart—pan, zoom, and follow links between people. The focus is on clarity and navigation, not academic citation workflows.

[SCREENSHOT]

### GEDCOM import

Bring in an existing tree from standard **GEDCOM** files (available during onboarding when the database is empty and you have administrator access). Individuals, relationships, and many common life events are mapped into the app’s model so you are not starting from scratch.

[SCREENSHOT]

### Home feed

A single timeline for **posts**, **photos**, and **videos** from people in your family—similar in spirit to a social feed, but without engagement metrics or algorithmic games. Share what is on your mind or let new media appear in the stream.

[SCREENSHOT]

### Profiles and life events

Each person can have a rich **profile**: photos, narrative context, and **events** (birth, death, residence, and custom milestones). Events can tie into **maps** so you can see where things happened when locations are available.

[SCREENSHOT]

### Media library

Upload and organize **photos** and **videos** with optional **folders** and **tags**. Images can be processed automatically (resize, thumbnails, quality settings). Videos are processed with **FFmpeg** (including transcoding for web-friendly playback). **Documents** are supported with configurable types and size limits.

[SCREENSHOT]

### Chat

**Rooms** and **messages** so family can talk in real time inside the same app—no need to route everything through external messengers.

[SCREENSHOT]

### Notifications

Stay aware of activity that matters to you through the app’s **notification** system (e.g. new messages or relevant updates).

[SCREENSHOT]

### Maps

Explore **events** or **photo locations** on a map when geographic data is present—useful for trips, heritage, and visualizing where your family’s story unfolded.

[SCREENSHOT]

### Administration

**Administrators** can manage users and roles, and work with **backups** of person-related data. The app is designed to be run **self-hosted** (for example with Docker and PostgreSQL), with optional **OpenID Connect** integration (using **Keycloak**) if you want centralized identity instead of built-in accounts alone.

[SCREENSHOT]

---

## Tech stack (overview)

- **ASP.NET Core** web application (**.NET 10**)
- **PostgreSQL** with **Entity Framework Core**
- **ASP.NET Core Identity** (with optional **OpenID Connect** / Keycloak)
- **Hangfire** for background jobs (e.g. media processing)
- **Docker** support for the app and database
- **Serilog** logging (including PostgreSQL sink options)

---

## Did I use AI for this?

Yes—shamelessly. Without AI, this project would not exist. It is something I have wanted to build for years but never had the time. Life is busy: work, family, and everything in between. I am a senior developer and have been coding since the mid-2000s—I know what good code looks like and I know what sloppy code looks like. So yes: I used AI as a **productivity tool**, but the **architecture and decisions are mine**, and I am the one maintaining it.

---

## Credits

Kinnect builds on excellent open work from the community:

| Project | Description | Repository |
|--------|-------------|------------|
| **family-chart.js** | D3-based interactive family tree visualization (npm package `family-chart`; used in the tree UI) | [github.com/donatso/family-chart](https://github.com/donatso/family-chart) |
| **GeneGenie.Gedcom** | .NET library for loading, parsing, and working with GEDCOM data | [github.com/TheGeneGenieProject/GeneGenie.Gedcom](https://github.com/TheGeneGenieProject/GeneGenie.Gedcom) |

Thank you to the authors and contributors of these and other projects this app is built on.
