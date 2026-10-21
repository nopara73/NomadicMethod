# Modifier presentation

Use the tile's white/black state to express Off/On. Off and the first enabled
state share one graphic. A second enabled capability may use another graphic;
never add a separate standing, crossed-out or unavailable graphic for Off.

| Control | Off and first enabled state | Second enabled state |
| --- | --- | --- |
| Locomotion | Walking figure | Running figure |
| Wall | Wall | No-feet-on-wall variant |
| Mirror | Tall mirror | Compact mirror |
| Elevation | Support | Jump platform |

Binary controls keep one graphic in both states. Floor uses its floor-exercise
figure and Hard Floor uses its hard-floor graphic. Their existing restrictions,
modifier flags and accessibility descriptions remain independent of presentation.

Feedback must name the actual selected capability. Elevation distinguishes
`equipment OFF: elevation`, `equipment ON: support` and
`equipment ON: jump platform`. Let feedback wrap within the viewport rather than
clipping the words that distinguish capabilities. Keep Android, web startup and
the loaded web app synchronized. Icon changes must not alter saved workout state.
