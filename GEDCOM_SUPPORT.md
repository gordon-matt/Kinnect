# GEDCOM support

Kinnect can import a GEDCOM once, while the tree is still empty, from **Settings → onboarding** as an administrator. Upload a `.ged` / `.gedcom`, or a `.zip` that contains the GEDCOM and the files it points at.

The uploaded `.ged` is stored byte for byte (under the file-storage `gedcom` folder, which is not served). A later export is meant to patch Kinnect's edits into that file. Today's export does not do that yet; it writes a new file from what Kinnect stores. Anything in the lists below under "not imported" is safe only because the original file is kept.

This page is the checklist for adding more later.

## People

Imported from the first `NAME` and from `SEX`:

- Given names and surname. A `GIVN` value such as `John, Fitzgerald` is two given names, so the comma is removed
- Sex: `M` male, `F` female, anything else (including `U`, and a missing sex) stored as unknown
- The individual's xref, kept as `GedcomId` so a later export can use the same id
- `NOTE` text on the individual, joined into the person's Notes field (4,000 characters). Kinnect has one notes field
- The first occupation, education, and religion, copied onto the profile fields as well as stored as events

Not imported:

- Further names, nicknames, aliases, prefix, suffix, and name type
- `CHAN`, `REFN`, `RIN`, `AFN`, record ids from other sites
- Submitter links

## Events and facts

Each of these becomes a life event, with the date (year, month, day when the parser can read it), the place name, a description, and the event note (4,000 characters). The description is the `TYPE` when the file has one, otherwise the text on the tag itself (`1 OCCU Clerk`, `1 EVEN` / `2 TYPE Enlisted`).

`BIRT` `DEAT` `BURI` `CREM` `CHR` `BAPM` `BARM` `BASM` `BLES` `CHRA` `CONF` `FCOM` `ORDN` `ADOP` `NATU` `EMIG` `IMMI` `CENS` `PROB` `WILL` `GRAD` `RETI` `OCCU` `EDUC` `RELI` `RESI` `CAST` `DSCR` `IDNO` `NATI` `NCHI` `NMR` `PROP` `SSN` `TITL` `FACT`

`EVEN` is the **Custom Event** type in the profile dropdown. The description is what kind of event it was.

A tag Kinnect does not have a name for is also stored as a Custom Event, with the original tag in the description (`_MILT`, and so on).

Family events:

- `MARR`, `DIV`, and `ENGA` dates are stored on the spouse link (engagement sets the engagement flag even when the date is empty)
- Every other family event (`ANUL`, `DIVF`, `MARB`, `MARC`, `MARL`, `MARS`, a family `CENS` or `RESI`, and so on) is copied onto each spouse, because Kinnect has no family-event table

Not imported on an event:

- Cause (`CAUS`), age, agency, religion, and the event's own address
- Map coordinates under `PLAC` / `MAP`. The event table has latitude and longitude columns; import does not fill them
- Marriage, divorce, and engagement places
- Source citations on the event

## Relationships

- `HUSB` and `WIFE` become a spouse link. The original family xref is not stored
- `CHIL` sets the child's father to the husband and mother to the wife

Not imported:

- `PEDI` (birth, adopted, foster, sealing) and which parent adopted the child
- Child order
- `ASSO` (godparent, witness, friend)
- LDS ordinances: `BAPL`, `CONL`, `ENDL`, `SLGC`, `SLGS`

## Contacts

From the individual's `ADDR`, `EMAIL`, `PHON`, `FAX`, and `WWW` (the reader folds that address onto a residence, and Kinnect reads it back from there). The same fields on an event address are included too. A residence that is only an address does not also become a blank timeline event.

- `EMAIL` (up to three), stored as an Email contact
- `PHON` (up to three), stored as a Phone contact
- `FAX` (up to three), stored as a Fax contact
- `WWW` (up to three), stored as a Website contact
- Street lines, city, state, postcode, and country, joined into one Address contact

Export writes those back as `EMAIL`, `PHON` (phone and mobile), `FAX`, `WWW`, and `ADDR`.

## Media

A `.zip` is required. A plain `.ged` imports the tree and reports the missing files. Paths are matched by exact path, by a file sitting next to the `.ged`, and by the longest matching tail, including case, unicode form, and `file:` URIs.

- Images (jpg, jpeg, png, gif, bmp, tif, tiff, webp) become photos. They are re-encoded for the gallery. EXIF latitude and longitude are kept when present
- The profile picture is the photo marked `_PRIM` (`Y`, `YES`, or `1`). If none is marked, it is the first image
- A photo can be tagged on every person who references it, and linked to the event it was attached to
- Videos (mp4, webm, mov, avi, mkv, m4v, mpeg, mpg, wmv, ogv) become videos. They are stored as uploaded and are not sent through ffmpeg
- Anything else that is a real file (PDF, Word, and so on) becomes a document. Audio files land here too; there is no audio library
- `TITL` on the multimedia record, and `TITL` nested under `FILE` (GEDCOM 5.5.1), becomes the title. A note on the multimedia record becomes the description
- Documents and videos are stored once. They are owned by the first person they were imported for; there is no tag list for them
- One file may be up to 100 MB

Counted as skipped or missing, with a warning on the import summary:

- Web addresses and `mailto:` references
- A path that names a folder rather than a file
- A file the zip does not contain
- A file that cannot be read

Not imported from a multimedia record:

- `BLOB`
- `_DATE`, `_TEXT`, and other custom media tags besides `_PRIM`

## Export today

`Export` writes a new GEDCOM 5.5.1 file from the database:

- Name, sex (`M`, `F`, or `U`), birth, death
- Occupation, education, and religion, either from the profile field or from the event
- Other timeline events, with date, place, `TYPE` set from the description, and the event note
- The person's notes
- Email, phone, mobile, fax, website, and address
- Families: husband and wife (a male spouse is written as `HUSB`), marriage date, divorce date, engagement, and children whose father and mother are that pair

It does not write photos, videos, documents, or anything in the "not imported" lists. It does not edit the stored original. That merge is the next piece of work, and it is why the original is kept.