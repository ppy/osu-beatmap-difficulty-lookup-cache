# osu-beatmap-difficulty-lookup-cache

A memory-based caching layer for beatmap difficulty (star rating and attribute) lookups which cannot be easily stored to a database.

# Current Versions

This is part of a group of projects which are used in live deployments where the deployed version is critical to producing correct results. The `master` branch tracks ongoing developments. If looking to use the correct version for matching live values, please [consult this wiki page](https://github.com/ppy/osu-infrastructure/wiki/Star-Rating-and-Performance-Points) for the latest information.

# Query API

## Attributes

```
curl -X POST -H "Content-Type: application/json" \
    -d '{ "beatmap_id": 129891, "ruleset_id": 0, "mods": [ { "acronym": "DT" } ] }' \
    http://localhost:80/attributes
    
{"aimStrain":4.578457906273151,"speedStrain":6.196480875019435,"approachRate":10.333333333333332,"overallDifficulty":9.777777777777779,"hitCircleCount":1646,"spinnerCount":2,"mods":[{"acronym":"DT","speedChange":1.5,"settingDescription":"","ranked":false}],"skills":[{},{}],"starRating":11.474577136820706,"maxCombo":2385}
```

`ruleset_id` and `mods` are optional.

## Star rating

```
curl -X POST -H "Content-Type: application/json" \
    -d '{ "beatmap_id": 129891, "ruleset_id": 0, "mods": [ { "acronym": "DT" } ] }' \
    http://localhost:80/rating
    
11.474577136820706
```

`ruleset_id` and `mods` are optional.

## Purge

```
curl -X DELETE \
    http://localhost:80/cache?beatmap_id=129891
```