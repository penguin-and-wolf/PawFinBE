# Pawfin Backend

A Jellyfin plugin (`com.penguinandwolf.pawfin`) that adds:

1. **Seasonals** – collection-like groups holding movies, series, episodes and collections, active between two dates (or on a single day, optionally repeating yearly), with their own order mode.
2. **Images, backgrounds and themes** for seasonals *and* existing collections.
3. **Next Up removal** – hide series/episodes from a user's Next Up list.
4. **Marathons** – save multiple ordered queues per user and resume each across devices.

All data is stored on the server in `<jellyfin data>/pawfin/`, so it is shared across every device.
Supports Jellyfin 12.1 and newer (.NET 10).

## Installation

After a GitHub release is published, add the repository for your Jellyfin version
under **Dashboard > Plugins > Repositories**:

| Jellyfin | Repository URL |
|---|---|
| 12.1+ | `https://raw.githubusercontent.com/penguin-and-wolf/PawFinBE/main/manifest-jf12.json` |

Then install **Pawfin Backend** from the catalog. Each release publishes separate
build and updates the Jellyfin 12.1+ manifest with its download and MD5 checksum.

## API

All routes need an authenticated Jellyfin user. Write operations on seasonals/assets need an administrator.

### Seasonals — `/Pawfin/Seasonals`

| Method | Route | Description |
|---|---|---|
| GET | `/Pawfin/Seasonals?activeOnly=&date=&includeItems=` | List seasonals |
| GET | `/Pawfin/Seasonals/{id}` | One seasonal with ordered items |
| POST | `/Pawfin/Seasonals` | Create: `{name, overview, startDate, endDate, recurring, orderMode, itemIds}` |
| PUT | `/Pawfin/Seasonals/{id}` | Update (null fields unchanged, `clearDates: true` removes dates) |
| DELETE | `/Pawfin/Seasonals/{id}` | Delete (also removes stored files) |
| POST / DELETE | `/Pawfin/Seasonals/{id}/Items?itemIds=a&itemIds=b` | Add / remove items |
| POST | `/Pawfin/Seasonals/{id}/Items/Order` | Body: array of ids in desired order |

Order modes: `Specific`, `Random`, `Chronological`, `ReverseChronological`, `Alphabetical`, `DateAdded`.

### Assets — `/Pawfin/{Seasonals|Collections}/{id}/…`

`GET Assets`, `POST|GET|DELETE Images/{Primary|Logo|Banner|Thumb|Art|Disc|Box|…}`,
`POST Backdrops`, `GET|DELETE Backdrops/{index}`, `POST ThemeSongs`, `GET|DELETE ThemeSongs/{index}`,
`POST ThemeVideos`, `GET|DELETE ThemeVideos/{index}`.
Uploads send the raw file as the body with a matching `Content-Type`.

### Next Up — `/Pawfin/NextUp`

`GET /Pawfin/NextUp?limit=` (filtered list), `GET /Pawfin/NextUp/Hidden`, `POST|DELETE /Pawfin/NextUp/Hidden/{itemId}`.

### Marathons — `/Pawfin/Marathons`

`GET` lists the user's saved marathons, `PUT` creates or updates one using its
`marathonId`, and `DELETE /Pawfin/Marathons/{marathonId}` removes only that queue.
The legacy `/Pawfin/Marathon` routes remain available for older app versions.

## Build

```
dotnet build
dotnet test
```
