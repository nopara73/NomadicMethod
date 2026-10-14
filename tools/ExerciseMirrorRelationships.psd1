@{
    # The reflection is part of the exercise, not merely an optional form aid.
    # Coverage is the minimum mirror height needed to perform the exercise.
    MirrorOnlyByCoverage = @{
        # A compact mirror that shows roughly the upper body is sufficient.
        UpperBody = @(

        )

        # The whole body must be visible, so these require a tall mirror.
        FullBody = @(

        )
    }

    # Continuous live self-view must substantially change execution under one
    # of these narrow criteria. Ordinary optional form checking is deliberately
    # insufficient. The current count is an audit result, never a target, quota,
    # ceiling, or reason to promote or demote an otherwise valid exercise.
    BenefitsGreatlyByCriterion = @{
        # The reflection continuously exposes guard, chamber, stance, strike
        # path, or defensive path in a technique-sensitive martial movement.
        TechnicalMartialArts = @(
        1024,
            92, 93, 97, 178, 180, 181, 182, 183, 204, 205, 220, 231, 245, 258,
            276, 278, 279, 280, 283, 285, 286, 287, 291, 294, 326, 327, 404, 473,
            534, 535, 536, 541, 543, 545, 546, 556, 575, 578, 583, 591, 681, 684,
            685, 687, 884, 885, 886, 887
        )

        # The reflection materially changes the intended whole-body line,
        # placement, or pose rather than merely making the movement observable.
        DanceAndAlignmentSensitivePoses = @(
            58, 105, 108, 190, 217, 232, 260, 524, 525, 526, 527, 528, 561, 609,
            610, 666, 790, 905
        )

        # Live feedback reveals knee, pelvis, trunk, or free-limb alignment in
        # demanding single-leg work where the relevant drift is hard to feel.
        ComplexSingleLegAlignment = @(
        1022,
            21, 96, 115, 148, 177, 186, 292, 367, 393, 529, 532, 537, 540, 542,
            996, 997
        )

        # The movement's main quality depends on seeing a plane, path, or
        # left-right symmetry error while it is happening.
        LivePlaneOrSymmetryCorrection = @(
        1014,
            246, 265, 268, 329, 481, 522, 523, 531, 917, 1008, 1010, 1013
        )

        # The reflection supplies an eye-level fixation target while also
        # making unintended trunk motion and the head-turn path visible.
        GazeStabilityFeedback = @(
            202
        )

        # The intended movement is a subtle pelvis/spine position change whose
        # accuracy is difficult to judge proprioceptively; a side reflection
        # makes the actual tilt continuously visible.
        SubtlePelvicPositionFeedback = @(
            916
        )
    }

    # This second axis records how much of the body must be visible for the
    # substantial benefit above. It must exactly partition the criterion list.
    BenefitsGreatlyByCoverage = @{
        UpperBody = @(
            1024,
            202, 204, 205, 220, 231, 245, 246, 258, 265, 268, 276, 278, 279, 280,
            283, 285, 286, 287, 291, 294, 326, 327, 329, 473, 481, 522, 523, 531,
            541, 543, 545, 546, 556, 591, 681, 884, 917, 1010, 1013
        )

        FullBody = @(
            1022,
            1014,
            21, 58, 92, 93, 96, 97, 105, 108, 115, 148, 177, 178, 180, 181,
            182, 183, 186, 190, 217, 232, 260, 292, 367, 393, 404, 524, 525, 526,
            527, 528, 529, 532, 534, 535, 536, 537, 540, 542, 561, 575, 578, 583,
            609, 610, 666, 684, 685, 687, 790, 885, 886, 887, 905, 916, 996, 997,
            1008
        )
    }

    # A mirror supplies no more than optional, ordinary form checking. Seeing
    # oneself or being able to compare against the demo does not qualify by
    # itself.
    Agnostic = @(
        1033, 1034, 1035, 1036, 1037, 1038, 1039, 1040, 1041, 1042, 1043, 1044, 1045, 1046, 1047, 1048, 1049, 1050, 1051, 1052, 1053, 1054, 1055, 1056,
        1032,
        1031,
        1030,
        1028, 1029,
        1027,
        1026,
        1025,
        1021,
        1020,

        1018,
        1017,
        1015,
        15, 16, 17, 19, 20, 31, 32, 37, 41, 47, 56, 59, 60, 94,
        95, 98, 99, 100, 101, 102, 103, 104, 107, 109, 110, 111, 112, 113,
        114, 116, 117, 118, 119, 120, 121, 122, 123, 124, 125, 126, 127, 128,
        129, 130, 131, 132, 133, 134, 135, 136, 137, 138, 139, 140, 141, 142,
        143, 144, 145, 146, 147, 149, 150, 151, 152, 153, 154, 156, 159, 160,
        161, 162, 163, 165, 166, 167, 168, 169, 170, 171, 172, 173, 174, 175,
        176, 179, 184, 185, 187, 188, 191, 192, 193, 194, 195, 196, 197, 198,
        199, 200, 201, 203, 211, 212, 213, 214, 215, 216, 218, 219, 223, 224,
        225, 227, 228, 230, 233, 234, 236, 237, 238, 239, 240, 241, 242, 248,
        251, 252, 253, 254, 255, 256, 257, 261, 262, 263, 264, 266, 269, 270,
        271, 272, 273, 274, 275, 277, 281, 282, 284, 288, 289, 290, 293, 295,
        296, 301, 302, 303, 304, 305, 307, 308, 309, 310, 311, 314, 315, 321,
        338, 340, 341, 377, 379, 389, 390, 391, 392, 394, 395, 396, 397, 398,
        399, 400, 401, 402, 403, 405, 406, 407, 408, 409, 410, 411, 413,
        414, 415, 416, 417, 418, 419, 420, 421, 422, 423, 424, 425, 426, 427,
        428, 429, 430, 431, 432, 433, 434, 435, 436, 437, 438, 439, 440, 441,
        442, 443, 444, 445, 446, 447, 448, 449, 450, 451, 452, 453, 454, 455,
        456, 457, 458, 459, 460, 461, 462, 463, 464, 465, 466, 467, 468, 469,
        470, 471, 472, 474, 475, 476, 477, 478, 479, 480, 482, 483, 484, 485,
        486, 487, 488, 489, 490, 491, 492, 493, 494, 495, 496, 497, 498, 499,
        500, 501, 502, 503, 504, 505, 506, 507, 508, 509, 510, 511, 512, 513,
        514, 515, 516, 517, 518, 519, 520, 521, 530, 533, 538, 539, 544, 547,
        548, 549, 550, 551, 552, 554, 555, 557, 560, 562, 563, 564, 565, 566,
        567, 568, 569, 570, 571, 572, 573, 574, 576, 577, 579, 580, 581, 582,
        584, 585, 586, 587, 588, 603, 608, 611, 612, 613, 614, 615, 616, 617,
        618, 619, 620, 625, 626, 632, 633, 636, 647, 648, 649, 654, 677, 678,
        683, 686, 701, 702, 703, 704, 712, 733, 740, 741, 742, 743, 744, 745,
        746, 747, 748, 750, 751, 752, 755, 756, 758, 784, 801, 804, 816, 818,
        825, 831, 834, 835, 836, 843, 845, 906, 910, 911, 913, 914, 915, 918,
        919, 939, 943, 948, 949, 954, 958, 960, 962, 969, 971, 973, 986, 987,
        993, 998, 999, 1000, 1001, 1002, 1003, 1004, 1005, 1006, 1007, 1009, 1011, 1012
    )
}
