# Nomadic Method disability-free longevity taxonomy

Status: research and implementation specification; no application or catalog changes are authorized by this document.
Taxonomy freeze: 2026-08-02, before the exercise catalog was opened.
Evidence search and catalog audit cutoff: 2026-08-02.

## Decision summary

- **Primary endpoint:** physical-disability-free survival: time alive without persistent loss of independence in a basic activity of daily living (ADL).
- **Classification unit:** trainable gross-motor physical-capacity families.
- **Final taxonomy:** Balance, Strength, Stamina, Stepping, Mobility.
- **Fixed order:** Balance → Strength → Stamina → Stepping → Mobility.
- **Scheduling rule:** repeat that five-capacity cycle and take its first *N* entries for an *N*-minute workout. Every category appears by minute five; every five-round block is balanced.
- **Qualification rule:** an exercise is tagged only when it meaningfully challenges or trains a capacity. Mere involvement does not qualify.

Nomadic Method can train a narrow part of physical intrinsic capacity. It cannot, by itself, deliver healthy ageing or prove longer disability-free survival.

## Research method

Research was completed through 2026-08-02 in four deliberately separated stages. First, the product workflow and data shape were inspected without opening the exercise catalog. Second, the endpoint and complete causal landscape were mapped from WHO frameworks, standardized disability and physical-performance measures, clinical consensus statements, randomized trials, systematic reviews, and longitudinal cohorts. Primary or authoritative sources were preferred; observational predictors, intervention effects, and expert/mechanistic judgment were recorded as different forms of evidence rather than pooled into a false score. Third, five competing organizing systems were evaluated against the criteria in the request, after which the classification unit, five-category taxonomy, and category order were written down and frozen. Only then was `exercises.json` opened and every catalog record audited against the frozen definitions.

Search themes combined healthy ageing, intrinsic/locomotor capacity, disability-free survival, ADL dependence, frailty, physical performance, mortality, falls, resistance/power training, gait/stepping adaptability, endurance, and flexibility. The synthesis is narrative because the underlying studies use different populations, exposures, interventions, comparators, follow-up periods, and outcomes. Citations appear beside the claims they support; no association is promoted to a trainable cause merely because it predicts the endpoint.

## 1. Endpoint

### 1.1 Primary endpoint

The primary endpoint is **physical-disability-free survival**: time from a defined baseline until either:

1. all-cause death; or
2. persistent physical disability, operationalized as severe difficulty, inability, or need for another person's help in at least one Katz basic ADL—bathing, dressing, toileting, transferring, walking, or feeding—confirmed in the same ADL approximately six months later.

For a study, report restricted mean years alive without persistent physical disability over a prespecified horizon, with death and persistent disability forming the composite endpoint. This adapts the durable physical-disability component used by ASPREE while omitting dementia from the primary composite because Nomadic Method's explicit target is physical capability. Dementia and cognition remain important secondary or out-of-scope determinants, not categories manufactured for this movement app. See the [ASPREE endpoint description](https://ams.aspree.org/public/study-overview/about-aspree/study-endpoints/) and [ASPREE trial report](https://pmc.ncbi.nlm.nih.gov/articles/PMC6426126/).

Major mobility disability—commonly inability to complete a 400 m walk—is a proximal secondary outcome. In LIFE, a multicomponent physical-activity program reduced major mobility disability (hazard ratio 0.82) and persistent major mobility disability (hazard ratio 0.72), but the intervention combined substantial walking, strength, balance, and flexibility work over years. It does not identify the effect of a single capacity or validate Nomadic Method's 45-second dose. See the [LIFE randomized trial](https://pmc.ncbi.nlm.nih.gov/articles/PMC4266388/).

### 1.2 Distinguishing neighboring constructs

| Construct | Working definition | Why it is not the primary endpoint |
|---|---|---|
| Lifespan | Chronological time alive. | Ignores whether added years are independent or severely disabled. |
| Healthspan | Time lived in a broadly defined state of good health or without major disease/disability. Definitions and thresholds vary. | Too broad and inconsistently operationalized for this catalog. |
| Disability-free survival | Time alive before a prespecified, persistent disability event. | **Chosen endpoint:** combines survival and retained independence. |
| Functional independence | Current ability to perform necessary ADLs and instrumental ADLs without personal help; assistive products do not necessarily negate independence. | A state and secondary outcome; it does not include survival time. |
| Care dependence | Need for sustained care/support after capacity and environmental supports no longer suffice for basic daily tasks. | Important but relatively advanced, context-dependent disability. |
| Frailty | Increased vulnerability to stressors because reserve is reduced; phenotype and deficit-accumulation models differ. | A risk state, not disability or independence itself. See the [Fried phenotype](https://pubmed.ncbi.nlm.nih.gov/11253156/) and [Rockwood deficit model](https://pubmed.ncbi.nlm.nih.gov/21093719/). |
| Intrinsic capacity | WHO's composite of a person's physical and mental capacities, including locomotor, cognitive, psychological, sensory, and vitality domains. | A broad determinant/intermediate construct; functional ability also depends on environment. |
| Quality-adjusted life expectancy (QALE) | Expected survival weighted by preference-based health-state utility. | Valuable for population/economic comparisons, but broader and more value-laden than physical independence. See the [National Academies definition](https://www.ncbi.nlm.nih.gov/books/NBK53336/?report=reader). |
| Functional reserve | Margin between a person's capacity and the demand of a valued task. | Useful design concept, but not a standardized endpoint. |

WHO defines healthy ageing as developing and maintaining the functional ability that enables wellbeing. Functional ability arises from intrinsic capacity, the environment, and their interaction; it is not synonymous with being disease-free. See [WHO Healthy Ageing](https://www.who.int/news-room/questions-and-answers/item/healthy-ageing-and-functional-ability), the [WHO ICF](https://www.who.int/classifications/international-classification-of-functioning-disability-and-health), and the [WHO ICF beginner's guide](https://cdn.who.int/media/docs/default-source/classification/icf/icfbeginnersguide.pdf?download=true&sfvrsn=eead63d3_4).

### 1.3 Secondary and intermediate outcomes

- Katz ADL and Lawton IADL independence.
- WHODAS 2.0 mobility and self-care domains; see the [WHO WHODAS resource](https://www.who.int/standards/classifications/international-classification-of-functioning-disability-and-health/who-disability-assessment-schedule).
- Major mobility disability and persistent major mobility disability using the 400 m walk definition.
- Falls and injurious falls, while recognizing that falls are a multifactorial failure mode rather than a capacity.
- Short Physical Performance Battery (SPPB), usual gait speed, 400 m or six-minute walk, strength/power, postural control, and active joint range as intermediate measures.

SPPB, gait speed, chair rise, and grip are useful risk markers. Their predictive validity does not establish that training a visible test component causes longer survival. The original [SPPB cohort](https://pubmed.ncbi.nlm.nih.gov/8126356/), a [pooled gait-speed/disability analysis](https://pmc.ncbi.nlm.nih.gov/articles/PMC4715231/), and a [physical-capability/mortality meta-analysis](https://www.bmj.com/content/341/bmj.c4467) support prediction, not a category-specific causal claim.

## 2. Evidence landscape and Nomadic Method's boundary

### 2.1 External frameworks

- **WHO ICF:** distinguishes body functions/structures, activities, and participation, all modified by personal and environmental factors. Capacity under standardized conditions differs from performance in a person's actual environment.
- **WHO Healthy Ageing:** functional ability is produced by intrinsic capacity plus environment and their interaction.
- **WHO ICOPE/intrinsic capacity:** locomotor, cognitive, psychological, sensory, and vitality capacities are relevant. Current WHO measurement definitions remain evolving and should be externally validated against outcomes such as care dependence, falls, hospitalization, long-term care, and mortality. See the [WHO intrinsic-capacity metadata](https://www.who.int/data/gho/indicator-metadata-registry/imr-details/percentage-of-older-people-with-high-intrinsic-capacity--over-the-past-year) and [ICOPE guidance](https://www.who.int/publications/i/item/integrated-care-for-older-people-%28-icope%29-guidance-for-person-centred-assessment-and-pathways-in-primary-care).
- **WHO locomotor capacity:** an expert working definition includes endurance, balance, muscle strength/function, muscle power, and joint function. See the [working-group report](https://pmc.ncbi.nlm.nih.gov/articles/PMC8894172/).
- **Sarcopenia consensus:** strength is more clinically salient than muscle mass alone, while poor physical performance indicates severity. See [EWGSOP2](https://pmc.ncbi.nlm.nih.gov/articles/PMC6322506/).

### 2.2 Complete causal and contextual map

| Factor family | Examples | Relationship to endpoint | Nomadic Method scope |
|---|---|---|---|
| Locomotor and gross-motor capacity | Force, postural control, gait/stepping, endurance, power, joint function | Contributes to reserve for mobility, transfers, self-care, and fall avoidance | **Direct but partial** |
| Habitual physical activity and sedentary exposure | Walking volume, resistance work, moderate/vigorous activity, sitting | Affects cardiovascular, metabolic, musculoskeletal, cognitive, and mortality risk | **Nomadic Method contributes only a small dose** |
| Disease and pain | Cardiovascular disease, cancer, diabetes, arthritis, neurological disease, chronic pain | Major causes/modifiers of disability and death | Outside catalog classification; requires prevention/clinical care |
| Cognition and psychological capacity | Memory, executive function, depression, motivation | Affects safe task performance, self-management, and independence | Outside a demonstration-only movement taxonomy |
| Sensory capacity | Vision, hearing, vestibular and proprioceptive function | Affects mobility, communication, and falls | Sensory treatment is out of scope; sensory use is embedded in balance |
| Vitality, nutrition, and body composition | Energy balance, malnutrition, protein adequacy, obesity | Modifies frailty, recovery, muscle adaptation, and disease | Outside scope |
| Bone and fracture susceptibility | Bone density/quality, osteoporosis, fall impact | Fractures are a major pathway to disability and care dependence | Important but not a category: quiet, no-impact, unweighted Nomadic Method work cannot assure an osteogenic dose |
| Manual function | Grip, dexterity, object manipulation | Supports feeding, dressing, medications, communication, and domestic tasks | Relevant but poorly represented by free-space hand gestures; task-specific object work conflicts with zero equipment |
| Health behavior and recovery | Smoking, alcohol, sleep, medication adherence | Affects disease, cognition, falls, and mortality | Outside scope |
| Healthcare and assistive support | Prevention, rehabilitation, medication review, assistive products | Prevents, treats, or compensates for capacity loss | Outside scope |
| Environment and social context | Housing, accessibility, transport, relationships, socioeconomic resources | Converts intrinsic capacity into actual functional ability | Outside scope |

Within its present constraints, Nomadic Method can credibly classify **standing gross-motor movements that meaningfully overload or practise a trainable physical capacity**. It cannot reproduce chair rise, floor recovery, carrying, stairs, long-distance gait, obstacles, uneven terrain, externally perturbed balance, or object-based dexterity. Outcomes such as falls, frailty, sarcopenia, fractures, pain, and cardiometabolic disease are not exercise categories.

### 2.3 Evidence interpretation

This specification uses four labels rather than a fabricated numeric score:

1. **Direct intervention evidence:** randomized evidence on disability, mobility disability, falls, or ADL.
2. **Intermediate intervention evidence:** randomized evidence that training changes a capacity or physical-function measure.
3. **Observed prediction:** cohort association with disability, care dependence, institutionalization, or mortality.
4. **Mechanistic or expert judgment:** plausible contribution, safety, sequence, or product fit without direct comparative endpoint trials.

Key signals are:

- LIFE supplies direct multicomponent intervention evidence for major mobility disability, not isolated category effects.
- Exercise reduces community fall rates by about 23%; balance/functional exercise and multicomponent programs have the clearest component-level evidence. See the [Cochrane falls review](https://www.cochrane.org/evidence/CD012424_exercise-preventing-falls-older-people-living-community) and [world falls guideline](https://pmc.ncbi.nlm.nih.gov/articles/PMC9523684/).
- Progressive resistance improves strength substantially and physical function more modestly. A review of 121 randomized trials reported improvements in gait, chair rise, and some complex ADLs, with studied programs commonly using progressive external resistance. See the [Cochrane resistance review](https://www.cochrane.org/evidence/CD002759_progressive-resistance-strength-training-improving-physical-function-older-adults).
- Power training may have a modest physical-function advantage over conventional strength training, but certainty and hard-disability evidence are limited. See the [power-training meta-analysis](https://pmc.ncbi.nlm.nih.gov/articles/PMC9096601/).
- Step/gait-adaptability training can improve stepping outcomes and may reduce falls, but many studied programs use targets, obstacles, cues, or supervision that Nomadic Method lacks. See the [step-training review](https://pubmed.ncbi.nlm.nih.gov/26746905/) and [gait-adaptability review](https://academic.oup.com/ageing/article/50/6/1914/6296913).
- Flexibility training improves range of motion; evidence that isolated flexibility training improves ADLs or prevents disability is limited and inconsistent. See the [flexibility review](https://pmc.ncbi.nlm.nih.gov/articles/PMC3503322/).
- WHO recommends varied multicomponent activity emphasizing functional balance and strength on three or more days per week for older adults. This supports a multicapacity design, not a claim that one Nomadic Method round meets the recommended dose. See the [WHO physical-activity recommendations](https://www.ncbi.nlm.nih.gov/books/NBK566046/?report=printable).

## 3. Correct classification unit

### 3.1 Candidate units

| Organizing unit | Endpoint relevance | Evidence | Nomadic Method trainability | Distinctness | Measurability | Short-workout fit | Novel-exercise stability | Main problem |
|---|---:|---:|---:|---:|---:|---:|---:|---|
| Anatomy / muscle groups | Medium | Medium | High | Low | Medium | High | Medium | Describes where work occurs, not the reserve being trained; compound movements fragment arbitrarily. |
| Physiological systems | High | High | Medium | Low | Medium | Medium | High | Neuromuscular, cardiovascular, sensory, and musculoskeletal systems co-activate in nearly every movement. |
| Functional abilities / ADLs | High | High | Low–medium | Medium | High | Low | Medium | Transfers, carrying, stairs, self-care, and domestic tasks depend on objects and environments Nomadic Method cannot reproduce. |
| Disability failure modes | High | Medium–high | Medium | Low | Medium | Low | Low | Falls, frailty, fractures, and care dependence are overlapping multicausal outcomes, not clean exercise stimuli. |
| Fine-grained performance components | High | High | Medium | Medium–high | High | Low–medium | High | Separating maximal force, power, local endurance, aerobic endurance, balance, gait adaptation, and every joint function over-fragments very short workouts. |
| **Gross-motor capacity families** | **High** | **High** | **High–medium** | **High–medium** | **High** | **High** | **High** | Boundaries still require explicit qualification rules and multi-tagging. |

### 3.2 Competing taxonomies developed before catalog inspection

1. **Anatomical reserve:** lower-limb, trunk, upper-limb, neck, hand. It is simple and trainable, but weakly aligned to independence; a squat, lunge, and martial kick get split or duplicated based on anatomy rather than adaptive demand.
2. **Functional-task reserve:** locomotion, transfers, reach/lift/carry, self-care manipulation, recovery/fall avoidance, sustained community activity. It is endpoint-proximal and measurable, but most tasks cannot be faithfully practised in a silent, standing, zero-equipment square.
3. **Failure-mode prevention:** falls, sarcopenia/frailty, exertional intolerance, joint restriction, gait failure, fracture susceptibility. It foregrounds harms that matter, but categories overlap, combine outcomes with mechanisms, and invite unsupported labels such as “prevents frailty.”
4. **Fine-grained capacities:** balance, maximal force, power, local muscular endurance, cardiorespiratory endurance, locomotor adaptability, coordination, and joint-specific mobility. It is scientifically descriptive, but power and local endurance cannot be cleanly isolated or dosed by most unweighted 45-second demonstrations, generic coordination has weak independent endpoint evidence, and the schedule becomes fragmented.
5. **Capacity families (selected):** Balance, Strength, Stamina, Stepping, Mobility, with multiple assignments and force subtypes. This retains externally recognizable constructs, links them to validated measures, fits the available movement format, and accepts novel practices without creating ad hoc categories.

The selected unit is therefore **trainable gross-motor physical-capacity family**. Practice, anatomy, motion shape, tempo, and hold/repetition remain descriptive metadata, not scheduling categories.

## 4. Frozen taxonomy

The five families were frozen before `exercises.json` was opened. They were not merged or created to satisfy catalog coverage.

### 4.1 Balance

- **Scientific definition:** ability to orient the body and keep or voluntarily recover its center of mass over its base of support during static posture, voluntary movement, and self-generated disturbance.
- **Include:** deliberately narrowed or changing base of support; sustained single-leg stance; substantial controlled weight shift; controlled turning; or another genuine postural-control challenge.
- **Exclude:** ordinary two-foot standing during unrelated arm, head, or trunk motion; an unweighted foot gesture without material stability demand.
- **Boundary:** Stepping is primary when initiating, sequencing, redirecting, or terminating steps is the main skill. Strength is primary when force production or local fatigue is limiting. Both may also be tagged.
- **Validated measurements:** SPPB standing-balance items; Four-Stage Balance Test; tandem or single-leg stance for narrow constructs; Berg Balance Scale or Mini-BESTest for broader standing balance. A WHO-linked review discusses validated locomotor measures and cautions that instruments often combine attributes: [COSMIN locomotor-measure review](https://pmc.ncbi.nlm.nih.gov/articles/PMC10615073/).
- **Endpoint relationship:** poor balance predicts adverse outcomes; balance/functional exercise reduces falls, a major disability pathway.
- **Evidence strength:** high for fall reduction; moderate for direct causation of disability-free survival.
- **Likely Nomadic Method trainability:** high for self-generated static and dynamic balance.
- **Limitations:** safe reactive/perturbation training is not feasible without supervision or support. Challenge and safety vary greatly by user; balance is placed first while fresh.

### 4.2 Strength

- **Scientific definition:** the muscular force-capacity family: ability to generate and control force, including functional/maximal force, rapid force (power), isometric force, and local muscular endurance under meaningful resistance.
- **Include:** substantial bodyweight leverage; self-resistance; a forceful hold; repeated loaded joint action likely to produce local fatigue; or deliberately rapid force against meaningful body inertia.
- **Exclude:** mere muscle activation, relaxed arm waving, finger choreography, an easy unloaded gesture, or a martial shape with no credible force or fatigue stimulus.
- **Boundary:** whole-body cardiorespiratory limitation belongs primarily to Stamina. Controlled end-range challenge belongs primarily to Mobility. A rapid squat or kick can receive Strength with a power facet; ordinary slow resistance work must not be mislabeled as power.
- **Validated measurements:** grip or joint dynamometry; five-times and 30-second chair stand as functional composite proxies; formula/instrument-derived sit-to-stand power or stair-climb power for the power facet.
- **Endpoint relationship:** force reserve supports transfers, stairs, gait, carrying, posture, and recovery from a loss of balance. Strength and chair-rise performance predict disability and mortality; progressive resistance improves strength and some physical-function/ADL outcomes.
- **Evidence strength:** high for strength and intermediate physical function; moderate for disability; low-certainty incremental evidence for power over conventional strength.
- **Likely Nomadic Method trainability:** moderate. Lower-body and self-resisted/isometric work can be meaningful; maximal strength, pulling, carrying, and progressive loading are constrained.
- **Limitations:** no external load or velocity measurement makes overload and power dosing imprecise. Catalog review must reject “moving a muscle” as sufficient.

**Why power is a facet, not a sixth scheduler family:** power is scientifically distinct from maximal force and must remain explicit in exercise metadata/rationale. It is grouped at the scheduling-family level because nearly every safe, equipment-free power movement also produces a force stimulus, most clips lack load/velocity needed for an isolated prescription, and a separate power round would over-fragment three-to-five-minute workouts. This decision preceded the coverage audit and was not made to reach ten exercises.

### 4.3 Stamina

- **Scientific definition:** ability to sustain rhythmic whole-body work through integrated cardiorespiratory, metabolic, and neuromuscular endurance.
- **Include:** continuous, rhythmic, large-muscle, multi-joint movement that plausibly elevates breathing and heart rate at the user's relative intensity for the round.
- **Exclude:** relaxed breathing, slow range work, an isolated neck/hand/arm gesture, or a local static hold.
- **Boundary:** Stepping is primary when directional foot placement is the principal learned demand. Strength is primary when local force/fatigue, rather than systemic effort, limits the movement.
- **Validated measurements:** cardiopulmonary exercise testing/VO₂peak; six-minute walk; validated two-minute walk or step tests.
- **Endpoint relationship:** supports walking reserve and broad cardiometabolic health. Cardiorespiratory fitness predicts mortality, but this is largely observational and not proof that a 45-second bout changes survival.
- **Evidence strength:** high for broad health/mortality association and exercise benefits; moderate for this specific disability endpoint, largely from multicomponent trials.
- **Likely Nomadic Method trainability:** moderate across accumulated rounds; low from a single isolated round.
- **Limitations:** a 3–20-minute session does not by itself meet studied aerobic-volume guidance; quiet/no-jump constraints cap intensity.

### 4.4 Stepping

- **Scientific definition:** locomotor capacity to initiate, sequence, redirect, and terminate weight-bearing steps with controlled cadence, clearance, direction, and weight transfer.
- **Include:** purposeful marching, walking patterns, multidirectional stepping, step-and-return, turns, cross-steps, or gait-pattern practice with meaningful foot placement and transfer.
- **Exclude:** a stationary kick, relevé, unweighted toe tap, or foot motion without meaningful step initiation/weight transfer; trivial choreography whose timing or placement is not challenging.
- **Boundary:** Balance is primary when stability on the support leg is the limiting demand. Stamina is primary when sustained systemic intensity dominates. Strength is primary when a lunge's force demand dominates its step skill.
- **Validated measurements:** usual four-metre gait speed; 400 m walk; Timed Up and Go; SPPB gait component; Functional Gait Assessment; Four Square Step Test and Figure-8 Walk for directional adaptability.
- **Endpoint relationship:** gait speed consistently predicts incident ADL and mobility disability; 400 m inability is an established major mobility-disability endpoint. Step/gait-adaptability interventions may reduce falls.
- **Evidence strength:** high prognostic evidence; moderate category-specific intervention evidence.
- **Likely Nomadic Method trainability:** moderate for voluntary foot placement and turns.
- **Limitations:** a flat 3 × 3 m space cannot reproduce distance, stairs, slopes, obstacles, uneven surfaces, environmental decisions, or true community ambulation.

### 4.5 Mobility

- **Scientific definition:** controllable, task-usable joint range of motion—not range for its own sake.
- **Include:** a dynamic or held movement that deliberately approaches a meaningful end range with control, or loads control through that range.
- **Exclude:** ordinary mid-range joint motion, an unloaded reach far from end range, or a pose that provides no meaningful range challenge.
- **Boundary:** Strength is primary when force rather than range is limiting. Balance is primary when maintaining the base of support dominates. Joint involvement during another movement is insufficient.
- **Validated measurements:** joint-specific active range-of-motion goniometry; chair sit-and-reach and back-scratch as limited functional proxies; task-specific range tests where validated.
- **Endpoint relationship:** adequate range enables dressing, reaching, gait, turning, and transfers.
- **Evidence strength:** high for improving range of motion; low-to-moderate and inconsistent for isolated flexibility training improving ADLs or disability.
- **Likely Nomadic Method trainability:** high for selected standing joints and movements.
- **Limitations:** mobility is joint-specific; there is no defensible single “global flexibility” score. More range beyond task needs is not necessarily better or safer.

### 4.6 Cross-cutting attributes, not categories

- Power, isometric work, and local muscular endurance are Strength facets.
- Agility, rhythm, reaction, and coordination are movement attributes; they qualify only through a meaningful Balance or Stepping demand.
- Flexibility is a contributor to controllable Mobility.
- Core, posture, muscles, and body regions are anatomy/strategy metadata.
- Hold versus repetition is playback metadata.
- Falls, fractures, frailty, sarcopenia, pain, and cardiometabolic health are outcomes or failure modes.
- Breathing, gaze, relaxation, and hand-seal drills receive no category unless the demonstrated movement independently meets a frozen capacity definition.

## 5. Category order

Ordering was performed after taxonomy freeze. No study validates an optimal order for five 45-second app rounds, so the result is a transparent ordinal judgment, not a numerical ranking of effect sizes.

### 5.1 Ordering method

Consider, in order:

1. direct intervention evidence on disability, major mobility disability, falls, or ADL;
2. consistent prospective prediction of disability/dependence;
3. plausible causal role and trainability under Nomadic Method constraints;
4. nonredundant contribution to short sessions;
5. safe within-session sequencing and expert judgment.

Observed prediction, plausible mechanism, intervention evidence, and product judgment must remain labeled separately:

| Category | Observed prediction | Intervention evidence | Plausible causal role | Product judgment |
|---|---|---|---|---|
| Balance | Standing-balance performance contributes to risk prediction. | High-certainty fall-rate reduction for balance/functional programs; direct disability-free-survival evidence is indirect. | Preserves stability and fall-avoidance reserve. | First while unfatigued. |
| Strength | Grip/chair-rise/force measures predict disability and mortality. | Strong strength gains and smaller function/ADL gains with progressive resistance. | Preserves transfer, stance, gait, and recovery force. | Second; co-equal core with Balance. |
| Stamina | Fitness and walking endurance predict health and mortality. | Direct disability evidence is mainly multicomponent. | Preserves sustained mobility and systemic reserve. | Third so every minimum workout contains aerobic/movement endurance. |
| Stepping | Gait speed strongly predicts incident disability. | Moderate evidence for step/gait-adaptability effects; LIFE is not category-isolated. | Preserves clearance, redirection, and controlled locomotion. | Fourth because it overlaps Balance/Stamina while adding specific gait skill. |
| Mobility | Restricted task-specific range can limit function. | Range improves; isolated ADL/disability transfer is uncertain. | Enables positions needed by other capacities and daily tasks. | Fifth because direct endpoint evidence is weakest. |

### 5.2 Fixed order

1. **Balance**
2. **Strength**
3. **Stamina**
4. **Stepping**
5. **Mobility**

Balance and Strength are a co-equal evidence core; their displayed order is a safety/product choice, not a claim of a precisely larger effect. Stamina's inclusion in the three-minute minimum is guideline-aligned. Stepping remains a distinct gait-skill family but shares substantial demand with Balance and Stamina. Mobility is last because its direct disability evidence is weakest, not because range is irrelevant.

## 6. Exact workout schedules

**Rule:** repeat `Balance → Strength → Stamina → Stepping → Mobility`; an *N*-minute workout uses the first *N* entries. No category repeats before all five appear. This exposes every category by minute five and needs no personalization, calendar rule, or rotation state. A fixed three-round workout cannot contain five categories; the minimum intentionally prioritizes the three strongest nonredundant guideline families.

| Minutes | Exact schedule |
|---:|---|
| 3 | Balance → Strength → Stamina |
| 4 | Balance → Strength → Stamina → Stepping |
| 5 | Balance → Strength → Stamina → Stepping → Mobility |
| 6 | Balance → Strength → Stamina → Stepping → Mobility → Balance |
| 7 | Balance → Strength → Stamina → Stepping → Mobility → Balance → Strength |
| 8 | Balance → Strength → Stamina → Stepping → Mobility → Balance → Strength → Stamina |
| 9 | Balance → Strength → Stamina → Stepping → Mobility → Balance → Strength → Stamina → Stepping |
| 10 | Balance → Strength → Stamina → Stepping → Mobility → Balance → Strength → Stamina → Stepping → Mobility |
| 11 | Balance → Strength → Stamina → Stepping → Mobility → Balance → Strength → Stamina → Stepping → Mobility → Balance |
| 12 | Balance → Strength → Stamina → Stepping → Mobility → Balance → Strength → Stamina → Stepping → Mobility → Balance → Strength |
| 13 | Balance → Strength → Stamina → Stepping → Mobility → Balance → Strength → Stamina → Stepping → Mobility → Balance → Strength → Stamina |
| 14 | Balance → Strength → Stamina → Stepping → Mobility → Balance → Strength → Stamina → Stepping → Mobility → Balance → Strength → Stamina → Stepping |
| 15 | Balance → Strength → Stamina → Stepping → Mobility → Balance → Strength → Stamina → Stepping → Mobility → Balance → Strength → Stamina → Stepping → Mobility |
| 16 | Balance → Strength → Stamina → Stepping → Mobility → Balance → Strength → Stamina → Stepping → Mobility → Balance → Strength → Stamina → Stepping → Mobility → Balance |
| 17 | Balance → Strength → Stamina → Stepping → Mobility → Balance → Strength → Stamina → Stepping → Mobility → Balance → Strength → Stamina → Stepping → Mobility → Balance → Strength |
| 18 | Balance → Strength → Stamina → Stepping → Mobility → Balance → Strength → Stamina → Stepping → Mobility → Balance → Strength → Stamina → Stepping → Mobility → Balance → Strength → Stamina |
| 19 | Balance → Strength → Stamina → Stepping → Mobility → Balance → Strength → Stamina → Stepping → Mobility → Balance → Strength → Stamina → Stepping → Mobility → Balance → Strength → Stamina → Stepping |
| 20 | Balance → Strength → Stamina → Stepping → Mobility → Balance → Strength → Stamina → Stepping → Mobility → Balance → Strength → Stamina → Stepping → Mobility → Balance → Strength → Stamina → Stepping → Mobility |

## 7. Catalog audit

### 7.1 Method

The taxonomy and order above were frozen before the catalog was opened. The audit then:

1. parsed every bundled exercise record;
2. copied practice and legacy muscle assignments only as historical fields, never as classification inputs;
3. applied the frozen inclusion/exclusion rules to the named and demonstrated movement;
4. sampled two separated frames from every human video and separately sampled every configured hold frame; ambiguous clips received denser review;
5. assigned all genuinely trained categories and one primary scheduling category when any category qualified; and
6. rated the disposition and confidence.

Disposition definitions:

- **Keep:** movement, naming, constraints, and media are sufficiently credible for later implementation.
- **Reconsider:** a real stimulus exists, but redundancy, dose, naming, difficulty, framing, or demonstration quality needs a product decision/fix.
- **Remove:** no frozen capacity is meaningfully trained, or the current demonstration violates a hard constraint/is materially inaccurate.
- **Strong representative:** Keep + High confidence + a clear primary stimulus and no unresolved constraint/media defect.

The complete row-level result is in [`exercise_capacity_audit.csv`](exercise_capacity_audit.csv). Counts below are based on the audited catalog as it exists; no row has been edited or removed.

| Category | Total tagged | Primary assignments | Strong primary representatives | Current deficit to 10 | Researched additions | Projected strong pool |
|---|---:|---:|---:|---:|---|---:|
| Balance | 109 | 49 | 34 | 0 | — | 34 |
| Strength | 120 | 76 | 61 | 0 | — | 61 |
| Stamina | 34 | 10 | 8 | 2 | Alternating Lateral Lunge with Knee Drive; Alternating Reverse Lunge with Knee Drive | 10* |
| Stepping | 51 | 15 | 12 | 0 | — | 12 |
| Mobility | 100 | 47 | 31 | 0 | — | 31 |

`Total tagged` is a semantic count and can include a nominally qualifying movement whose current record is later excluded for a constraint or media defect. `Strong primary representatives` is the conservative, currently usable coverage count. The full audit contains **154 Keep, 33 Reconsider, and 49 Remove** decisions across all 236 records.

Stamina is the sole present deficit. Two real, naturally alternating, low-impact movements were researched to close it; exact human demonstrations have been located, but the source videos still require download, edit, complete-playback verification, and app-loop QA. The projected total of 10 therefore remains conditional and must not be treated as selectable catalog inventory until that later media/catalog work is approved. Details and source links are in [`new_exercise_candidates.csv`](new_exercise_candidates.csv).

Coverage is judged on **strong primary representatives**, because the scheduler needs ten credible, category-defining choices—not ten incidental tags. Rows marked Reconsider or Remove do not satisfy that minimum. `new_exercise_candidates.csv` contains only the real named movements needed to close any resulting deficit; candidates are not catalog additions and require accurate human media before selection.

## 8. Implementation specification

This section describes a future implementation. It is not implemented in this research phase.

### 8.1 Domain model

Replace `MuscleGroup` with stable, nonlocalized identifiers:

```csharp
public enum CapacityCategory
{
    Balance = 1,
    Strength = 2,
    Stamina = 3,
    Stepping = 4,
    Mobility = 5,
}

public enum StrengthStimulus
{
    ControlledForce = 1,
    Power = 2,
    Isometric = 3,
    LocalEndurance = 4,
}
```

For a selectable exercise:

- `CapacityCategory[] Capacities` is nonempty, unique, and contains every meaningful assignment.
- `CapacityCategory PrimaryCapacity` is required and must be present in `Capacities`.
- `StrengthStimulus[] StrengthStimuli` is nonempty only when Strength is assigned; it preserves power as explicit metadata without adding a scheduler family.
- Existing `Id`, `Name`, `Video`, `Practice`, `MotionProfile`, `Mode`, `HoldFramePercent`, `Score`, and hard-constraint fields remain.
- Audit disposition/confidence are documentation/curation metadata, not runtime claims. Only approved selectable entries ship in a later catalog change.

Example future JSON shape:

```json
{
  "id": 15,
  "name": "Alternating Skater-RDL Balance",
  "video": "exercise_videos/exercise_0015.mp4",
  "capacities": ["Balance", "Strength", "Mobility"],
  "primaryCapacity": "Balance",
  "strengthStimuli": ["ControlledForce", "LocalEndurance"],
  "practice": "Balance training",
  "motionProfile": "HipOpenClose",
  "mode": "Repetition",
  "holdFramePercent": 0,
  "score": 0,
  "onlyFeetTouchGround": true,
  "shoeAgnostic": true,
  "maxSpaceMeters": 3,
  "equipment": "None",
  "silent": true
}
```

### 8.2 Database schema and migration

Use the next database version and migrate transactionally:

- Retain `exercises` with stable `id` primary key and unique `name`/`video`.
- Replace `exercise_muscle_groups` with `exercise_capacities(exercise_id, capacity, is_primary)`; primary key `(exercise_id, capacity)`, foreign key cascade, a closed capacity check, and exactly one `is_primary = 1` row per selectable exercise.
- Add `exercise_strength_stimuli(exercise_id, stimulus)` with a closed value check and unique pair.
- Index `(capacity, is_primary, exercise_id)` and `(score DESC)`.
- Before rebuilding, read `id`, `name`, and `score`. Restore a score only when both stable ID and name match; otherwise use the catalog's initial score. Never transfer a score between identities.
- Preserve the migration in one transaction and roll back completely on validation/insertion failure.
- Remove the legacy join table only inside that successful transaction.

Catalog validation must require:

- unique ID, name, and media path;
- all current hard constraints and valid hold/repetition metadata;
- nonempty unique capacities and primary membership;
- exactly one primary capacity;
- valid Strength facets, with at least one when Strength is assigned and none otherwise;
- at least five selectable exercises under every real on/on, on/off, off/on,
  and off/off UI state for each pair of workout modifiers and every scheduling
  category, with off relaxing that modifier's requirement and without an
  additional fixed ten-exercise rule;
- for each modifier alone and on both conditional edges of every modifier pair,
  enabling it excludes at least five exercises or 5% of the prior candidate
  pool, whichever is larger, and affects at least 10% of canonical buckets;
- referenced media present and decodable; and
- a manual release gate confirming an accurate moving human demonstration and correct final freeze frame for holds.

### 8.3 Persistent workout state

The old dictionary keyed by muscle group cannot represent repeated capacity rounds. Store a full 20-slot lineup:

```text
stateVersion
lastWorkoutMinutes
activeWorkoutMinutes
selectedSlots: [{ slotIndex, exerciseId }]
outcomes: [{ slotIndex, kept }]
pendingRest: { slotIndex, exerciseId, endsAt, remaining, pausedByUser, kept, scoreApplied } | null
workoutCompleted
completionAcknowledged
```

- Slot category is derived from the immutable 20-round schedule, not persisted as duplicate truth.
- Persist exercise IDs, not names; names may change while identity and score remain stable.
- Build/repair all 20 slots even when a shorter duration is selected. An active workout uses the prefix matching its duration.
- A slot is valid only if its exercise's `PrimaryCapacity` equals that slot's scheduled capacity.
- Preserve `lastWorkoutMinutes`, defaulting to 10 only when absent/invalid.
- Persist one active audit record plus append-only completed/interrupted workout
  history. Each record snapshots the starting lineup and Keep set, every
  Shuffle, every completed block (including demand, canonical muscles, cues,
  sequence position, and set position), and every final Keep/reject decision.
  Preserve names and anatomical metadata as historical snapshots so later
  catalog edits cannot rewrite prior sessions. Finalization is idempotent and
  must never apply or alter scores itself.

### 8.4 Selection and duplicate prevention

For each slot in order:

1. keep its current valid exercise unless that slot was marked for replacement;
2. otherwise use exercises whose **primary** category equals the scheduled category;
3. exclude the current exercise and every exercise already used in another of the 20 slots;
4. find the highest score among remaining candidates; and
5. choose uniformly at random from that highest-score bucket.

The catalog's pairwise modifier floor, quadratic materiality check, and separate
whole-lineup matching check must make strict uniqueness succeed for every
validated profile containing at most two enabled modifiers. Pairwise validation
does not prove an arbitrary three-or-more-modifier intersection. Treat failure
for a validated profile as invalid catalog/state, not as permission to duplicate
silently.
Inject a deterministic random source in tests.

Multi-category tags remain scientifically and analytically useful, but the required primary assignment prevents one exercise from competing in several scheduling pools and keeps coverage auditable.

### 8.5 Scoring, rest, and replacement

- Every atomic sequence block is 45 seconds of exercise and 15 seconds of rest.
- Atomic sequences are never excluded by duration. Exact block capacity decides
  whether they fit. Blocks with genuinely different primary workout groups may
  satisfy those slots together; same-primary side or direction blocks consume
  time without claiming extra slots. All blocks stay consecutive even when the
  surrounding group order shifts.
- Long-workout extra sets first respect phase-specific rejection feedback, then
  favor placements with the fewest current sets. Within that layer, an
  exact-slot Keep precedes every unkept choice, including when the kept sequence
  has multiple blocks. One-block sequences then precede multi-block sequences,
  followed by hard-work and workout-order priority. Exact remaining-duration
  feasibility may skip a candidate that cannot fit.
- Every retained record must be declared in exactly one mandatory sequence or
  in the explicit standalone inventory. There is no implicit standalone
  fallback; generation rejects unreviewed, overlapping, or orphaned records.
- Repetition and isometric-hold variants remain independent selectable
  movements. A shared movement identity prevents duplication within one
  workout; it does not force the variants into one mandatory sequence.
- For candidate priority, a sequence uses the hardest member whose primary
  muscle belongs to the slot being filled. Recovery timestamps are still
  recorded from each completed block's own exercise identity.
- On days 4, 8, 12, and so on of an uninterrupted local-calendar daily streak,
  or whenever Light is enabled manually, global assignment first maximizes
  slots covered by all-demand-`0` sequences. Saved scores and Keeps rank those
  light choices; harder work fills only anatomical slots the compatible
  demand-`0` catalog cannot cover. No preference value is rewritten.
- The soft within-session muscle rebalancer assigns primary/secondary workload
  of 0.25/0.125 for demand `0`, 0.5/0.25 for demand `1`, and 1/0.5 for demand
  `2`. Each distinct identity counts once per sequence set: repeated side or
  direction blocks do not double-count it, different linked identities do, and
  a later set counts again.
- Canonical workload is summed independently into the existing 3-, 5-, 7-,
  10-, 15-, 20-, and 30-minute muscle resolutions. The target at each is a
  weakest bucket at least 25% of its strongest. Nomadic Method applies one legal
  replacement that lexicographically improves the sorted shares, recalculates
  every resolution, and repeats until all pass, no improvement exists, a lineup
  repeats, or 30 passes complete.
- Exact-slot Keeps are frozen and unavailable as replacement candidates. A
  candidate must not lower the saved score of any displaced slot; modifier,
  recovery, Mirror, atomic-sequence, movement-uniqueness, exact-duration, and
  repeated-set constraints continue to apply. This is a best-effort lineup
  adjustment, not a persisted score or a catalog-filling quota.
- Keep is the only rest decision, and a sequence exposes it only after its final
  block and final repeated set.
- Intermediate sequence rests are neutral: they hide Keep, apply no score, and
  automatically start the next 45-second block when their 15-second timer ends.
- If Keep is tapped during the final rest window: score is unchanged and that
  slot retains the exercise.
- If the final rest resolves without Keep: decrement every distinct exercise
  identity in that sequence exactly once and mark only that sequence slot for
  replacement.
- On workout completion or interruption finalization, replace every marked slot from the highest-score bucket of the same primary category.
- Kept/unreached slots remain unchanged. An all-kept workout replaces nothing.
- Persist score updates before clearing pending-rest state; use `scoreApplied` or an equivalent transaction/idempotency guard so process recreation cannot decrement twice.

### 8.6 Mid-workout setup

- Expose one unobtrusive setup control in the exercise header. Opening it
  pauses an active movement or rest and shows the ordinary setup screen rather
  than adding modifier controls to the workout player.
- Lock duration for the active workout. Modifiers remain editable and the main
  action becomes Resume.
- Preserve every completed selection. Replan the unfinished atomic selection
  currently on screen with the complete target modifier profile; compatibility
  alone must not pin it in place. If normal target-profile selection chooses the
  same exercise, preserve its checkpoint and allocated sets. If a restriction
  or newly enabled equipment preference chooses another exercise, discard the
  old partial countdown and return the replacement in Ready state with a full
  timer. A block already completed and resting remains historical work rather
  than being rewritten retroactively. Replan all other unstarted selections
  without starting another workout.
- Record each applied modifier transition and its complete resulting plan in
  the active session log. Keeps, downvotes, and completed history remain
  untouched by the transition.

### 8.7 Abrupt close

An actively timed movement or pending rest resumes after process recreation:

- Completed rest decisions remain persisted.
- If the app closes during a running pending rest, restore that rest using its persisted absolute deadline. If the deadline already passed, resolve its persisted `kept` value immediately: tapped means keep; not tapped means one decrement/replacement.
- If the user pauses a pending rest, replace its deadline with the exact remaining duration and persist the user-pause state. Process recreation must keep Rest paused until the user explicitly resumes it; resuming creates a new deadline from that stored duration.
- When movement starts or resumes, persist its round, remaining duration, and a wall-clock deadline. On backgrounding or user pause, replace the deadline with the exact paused remaining duration and persist whether the pause was user initiated.
- If the process dies during Move, restore that same round and remaining time. A still-valid foreground deadline may account for time before the crash; an expired deadline falls back to the last safe stored duration so unseen background time is never credited. Do not score the restored movement.
- If it closes during Ready before movement begins, record no outcome for that unfinished round and return to duration selection under the existing interruption-finalization path.
- When an interruption is finalized rather than restored, apply replacements for all resolved not-kept rounds, retain kept and unreached selections, clear active progress, and preserve last duration.
- Initialization must be idempotent after process death at every write boundary.

### 8.8 Legacy-state compatibility

On the first capacity-model launch:

1. load the version-4 state through a compatibility DTO;
2. resolve any persisted pending-rest decision under the old rules exactly once so its score is respected;
3. preserve valid last-used duration and all database scores;
4. map legacy selected names to IDs, then greedily retain each exercise in the first unfilled new slot matching its audited primary category, without duplicates;
5. fill remaining slots from highest-score buckets; and
6. discard any active legacy workout progress and show duration selection.

Unqualified/removed exercises retain their historical database identity only for migration lookup; they must not enter the new selectable pools. A failed migration rolls back and must not erase the old state.

### 8.8 UI terminology and progress

- Replace “muscle group” with **capacity** in code-facing concepts and user-visible accessibility text.
- User labels are exactly Balance, Strength, Stamina, Stepping, and Mobility; explanations belong in documentation/onboarding only if later requested, not on the focused workout screen.
- Duration remains 3–20 minutes, defaulting to the last valid value or 10.
- Show logical exercise progress immediately and accessibly. A multi-block
  sequence and all of its repeated sets count once while their individual
  45-second work blocks remain visible in the execution timeline. Repeated sets
  use separate outlined groups so they cannot be mistaken for additional
  side, direction, or linked-exercise blocks in one set.
- Keep the human demonstration as the primary visual; preserve existing Ready, Move, Rest, and Done states, testing shortcut, hold/repetition playback behavior, one rest action, and one Done action.
- Do not imply clinical efficacy, “years added,” or a personalized prescription.

### 8.9 Accessibility and safety

- Maintain at least 48 dp touch targets, sufficient contrast, logical TalkBack order, descriptive control labels, and layouts that tolerate large fonts without clipping.
- Announce capacity, exercise, phase, round, and remaining time without repeatedly interrupting the user.
- Pair beep cues with visible state changes and optional haptic feedback; never make sound the sole signal.
- Respect reduced-motion/system animation settings for decorative transitions while keeping essential timing state visible.
- Give videos concise content descriptions naming the demonstrated action; do not narrate anatomical claims not present in the exercise name.
- Preserve screen-on behavior during an active workout and system-bar/inset handling.
- Do not present unsupported single-leg/reactive balance to users who cannot perform it safely; this remains a product safety limitation because Nomadic Method has no assessment or support equipment.

### 8.10 Verification plan

Automated tests should cover:

- exact category cycle and every 3–20 schedule in this document;
- taxonomy parsing, primary-membership, Strength-facet, constraint, media, and ≥10-primary coverage validation;
- 20-slot construction, prefix behavior at every duration, uniqueness, repair, and score-bucket randomness with a deterministic seed;
- keep/no-keep scoring, exactly-once persistence, replacement only within primary category, and no replacement after all-kept sessions;
- abrupt close during Ready, Move, tapped Rest, untapped Rest, completion, and every persistence boundary;
- database rollback plus ID/name score preservation;
- version-4 state migration, invalid/corrupt state recovery, and last-duration preservation;
- repetition looping and hold playback settling/freezing at the audited frame;
- UI progress/category labels, TalkBack order, large-font clipping, contrast, touch targets, reduced motion, and supported Android API levels.

Manual release checks should play every video on a physical phone, verify footwear/space/noise/equipment/ground-contact constraints, inspect every hold through the complete 45 seconds, and exercise all duration/close/reopen paths. Scientific validation would additionally require longitudinal outcomes and adequate training dose; app tests cannot establish that.

## 9. Risks, limitations, and unresolved decisions

- No direct evidence shows that Nomadic Method's exact dose extends physical-disability-free survival.
- Most positive trials used progressive, supervised, individualized programs over weeks to years, with much greater weekly volume and often equipment.
- Prognostic markers may reflect disease and reverse causation; improving a marker does not guarantee changing the endpoint.
- The five families interact. Multi-tagging is more honest than forced exclusivity, but the primary category is still a scheduling simplification.
- Grouping power into Strength is a product-level family decision. Preserve the explicit facet and revisit only if future velocity-aware programming can prescribe it distinctly.
- Mobility has the weakest direct endpoint evidence and is joint-specific.
- Nomadic Method's constraints particularly limit progressive upper-body pulling/loading, carrying, bone loading, sustained locomotion, stairs, object manipulation, and safe reactive balance.
- A user who always selects three minutes will receive only the three prioritized categories. Avoiding that would require cross-session rotation state, explicitly rejected here in favor of a simple deterministic schedule.
- Audit confidence is based on names plus sampled human video frames, not biomechanical measurement or clinical dosing. Reconsider rows need full playback/product review before future catalog implementation.
- Individual contraindications, disease, pain, pregnancy, disability, and fall risk are not assessed. This is not a clinical prescription.

## 10. Documentation outputs

- [`exercise_capacity_audit.csv`](exercise_capacity_audit.csv): one row per current catalog record.
- [`new_exercise_candidates.csv`](new_exercise_candidates.csv): only real named candidates needed to close strong-primary coverage deficits, with media status.
- [`../AGENTS.md`](../AGENTS.md): concise future exercise-selection rules linked back to this specification.
