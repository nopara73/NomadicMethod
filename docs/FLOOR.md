# Floor allowance and contact rules

Floor is an equipment allowance, initially off. Off admits only movements with
feet-only ground contact. On also admits floor movements; standing exercises
remain available. Wall and sole-on-wall permissions remain independent.

The Android and web catalogs must contain at least **201 distinct floor session
movements**, including at least **21 distinct movements that require both Floor
and Wall**, plus at least **21 distinct movements that require both Floor and
locomotion** (Room or Field travel). Both requirements must occur in the same
sequence member: pairing a stationary floor exercise with a standing walk does
not satisfy Floor + Locomotion. A floor-only member and a wall-only member in one
sequence do not satisfy Floor + Wall. Sides, directions, repetitions, holds, aliases,
sets, and blocks never increase either count. The entire atomic sequence must be
selectable under one consistent, relaxed equipment and clothing profile.

## Physical contact

`ExerciseFloorContact` records every contact in the complete demonstration,
including its transitions. It does not describe trained muscles.

| Setting | Contacts excluded |
| --- | --- |
| Floor off | Every ground contact except feet |
| Upper-body clothing off | Torso, chest, back, shoulders |
| Hard Floor on | Knees, elbows, head, torso, chest, back, shoulders |

Hands and forearms may touch the floor while shirtless. Elbows remain permitted
on a soft floor. Existing clothing requirements and hard-floor traction, impact,
and concentrated-loading restrictions also apply. Floor on does not override
Silence, Shy, Insect, locomotion space, Mirror, Wall, or sole-on-wall permissions.

Unknown, empty, or inconsistent contact metadata is rejected. A legacy feet-only
exercise may retain its safe feet-only default; missing metadata never admits a
floor movement. Canonical database reseeding supplies reviewed contacts while
preserving stored scores and workout state.

The Floor allowance is one binary toggle: a standing person means off, and a
person in a high plank means on. Its icon depends only on Floor; the accessible
description still states the applicable clothing and surface restrictions.
Floor softness retains its separate hard/soft icons.

The Movement context category contains space size, Insect, Floor allowance,
and floor surface, in that order: available space, movement style, permission
to use the floor, then the surface condition. Clothing, Silence and Shy remain
under workout conditions; Light remains intensity; Wall and Mirror remain
available equipment. The same grouping and order appear in setup and active
workout settings on Android and web.

## Validation

`tools/ExerciseFloorContacts.psd1` is the explicit contact partition for the full
catalog. Generation and `tools/Test-ExerciseFloorContactAudit.ps1` enforce the
partition, physical restrictions, and all three inventory minimums. Android database
validation, web startup, native tests, web tests, and the Pages workflow enforce
the same minimums.

Native and web tests share `web/tests/fixtures/floor-contact-cases.json` and cover
the eight Floor/clothing/surface combinations, threshold boundaries, duplicate
movement identities, impossible sequences, and separate Wall/sole permissions.
Integration tests cover changing physical setup during Ready and movement while
retaining the session, completed work, scores, and Keeps. Migration tests retain
unfinished Ready, movement, and Rest checkpoints from the preceding catalog.

Floor participates in the existing quadratic single/pair availability checks.
Its global inventory does not create a floor-only quota in each muscle bucket.
Final packaged media must also pass the project's actual-workout review before
publication.
