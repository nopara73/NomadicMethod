# Every retained catalog record receives one explicit scheduling decision:
# either it belongs to exactly one mandatory sequence below or it appears in
# StandaloneIds. Nothing silently defaults to a standalone exercise.
@{
    # Ordered exercise families that are valuable only when every block is
    # completed in one uninterrupted sequence. Each member expands to its own
    # reviewed 45-second side/direction blocks; NomadicMethod inserts 15 seconds between
    # blocks and records one outcome only after the final block.
    Sequences = @{
        '96' = @(96, 540)                 # Figure-four squat sides, then alternating integration
        '115' = @(115, 532)               # Pistol squat sides, then alternating integration
        '178' = @(178, 535)               # Crescent-kick sides, then alternating integration
        '179' = @(179, 539)               # Hip-hinge rear-leg-raise sides, then alternating integration
        '180' = @(180, 534)               # Front-snap-kick sides, then alternating integration
        '181' = @(181, 536)               # Side-thrust-kick sides, then alternating integration
        '211' = @(211, 213)               # Wrist-flexion and wrist-extension stretches on both sides
        '214' = @(214, 755)               # Inward, then outward wrist circles on both sides
        '220' = @(220, 543)               # Rising-block sides, then alternating integration
        '223' = @(223, 756)               # Inward, then outward controlled wrist circles on both sides
        '252' = @(252, 253, 254)          # Three calf-raise foot angles
        '264' = @(264, 406)               # Synchronous arm circles, then windmill-style integration
        '285' = @(285, 545)               # Outward-block sides, then the same outward action alternating
        '288' = @(288, 758)               # Forward, then backward knee-and-ankle circles on both sides
        '291' = @(291, 294)               # Inward, then outward alternating knife-hand strikes
        '302' = @(302, 304)               # Forward, then backward arm circles while marching
        '307' = @(307, 310)               # Self-resisted neck flexion, then extension
        '367' = @(367, 529)               # Single-leg deadlift sides, then alternating integration
        '392' = @(392, 399, 400)          # Three complete breathing-squat patterns
        '393' = @(393, 537)               # Deadlift-to-runner-march sides, then alternating integration
        '415' = @(415, 416)               # Fixed-thumb nod and tilt planes on both sides
        '420' = @(420, 421, 426)          # Jumping-, seal-, and cross-jack series
        '465' = @(465, 445)               # Single-leg jump-rope sides, then changing-feet integration
        '491' = @(491, 501)               # Fixed-thumb gaze stabilization with nods and turns
        '500' = @(500, 505, 506)          # Jaw opening, side glide, and forward glide
        '502' = @(502, 503)               # Levator-scapulae and upper-trapezius stretches on both sides
        '566' = @(566, 581, 582)          # Parallel, toes-in, then toes-out calf raises
        '610' = @(610, 232)               # Warrior II, then extended side angle on both sides
        '612' = @(612, 530)               # Lateral-leg-lift sides, then alternating integration
        '617' = @(617, 620)               # Forward, then backward side-leg circles on both sides
        '742' = @(742, 338)               # Triceps stretch, then triceps stretch with side bend
        '784' = @(784, 969, 1000)         # Mountain, chair, and standing forward-fold holds
        '834' = @(834, 914)               # Diagonal knee-pull variations on both sides
        '910' = @(910, 962)               # Pilates knee-lift twists followed by alternating knee taps
        '948' = @(948, 949)               # Natural wood-chop phases on both sides; one session-movement family
    }

    # These records were reviewed and deliberately remain complete one-member
    # scheduling units. Sequence roots and hidden members must not appear here.
    StandaloneIds = @(
        1621, 1701, 1702, 1703, 1704, 1705,
        1506, 1507, 1508, 1509, 1516, 1521, 1522, 1523, 1524, 1525, 1531, 1532, 1533, 1534, 1535, 1540, 1602, 1603, 1604, 1612, 1613, 1614, 1622, 1623, 1624, 1632, 1633, 1634, 1642, 1643, 1644,
        1431, 1432, 1433, 1434, 1435, 1436, 1437, 1438,
        1401, 1402, 1403, 1404, 1405, 1406, 1407, 1408, 1409, 1410,
        1411, 1412, 1413, 1414, 1415, 1416, 1417, 1418, 1419, 1420,
        1421, 1422, 1423, 1424, 1425, 1426, 1427, 1428, 1429, 1430,
        1303,
        1302,
        1301,
        1300,
        1299,
        1298,
        1297,
        1296,
        1295,
        1294,
        1272, 1273, 1274, 1276, 1277, 1278, 1279, 1280, 1281, 1282, 1283, 1284, 1285, 1286,
        1287, 1288, 1289, 1290, 1291, 1292, 1293,
        1057, 1058, 1059, 1060, 1061, 1062, 1063, 1064, 1065, 1066, 1067, 1068, 1069, 1070,
        1071, 1072, 1073, 1074, 1075, 1076, 1077, 1078, 1079, 1080, 1081, 1082, 1083, 1084,
        1085, 1086, 1087, 1088, 1089, 1090, 1091, 1092, 1093, 1094, 1095, 1096, 1097, 1098,
        1099, 1100, 1101, 1102, 1103, 1104, 1105, 1106, 1107, 1108, 1109, 1110, 1111, 1112,
        1113, 1114, 1115, 1116, 1117, 1118, 1119, 1120, 1121, 1122, 1123, 1124, 1125, 1126,
        1127, 1128, 1129, 1130, 1131, 1132, 1133, 1134, 1135, 1136, 1137, 1138, 1139, 1140,
        1141, 1142, 1143, 1144, 1145, 1146, 1147, 1148, 1149, 1150, 1151, 1152, 1153, 1154,
        1155, 1156, 1157, 1158, 1159, 1160, 1161, 1162, 1163, 1164, 1165, 1166, 1167, 1168,
        1169, 1170, 1171, 1172, 1173, 1174, 1175, 1176, 1177, 1178, 1179, 1180, 1181, 1182,
        1183, 1184, 1185, 1186, 1187, 1188, 1189, 1190, 1191, 1192, 1193, 1194, 1195, 1196,
        1197, 1198, 1199, 1200, 1201, 1202, 1203, 1204, 1205, 1206, 1207, 1208, 1209, 1210,
        1211, 1212, 1213, 1214, 1215, 1216, 1217, 1218, 1219, 1220, 1221, 1222, 1223, 1224,
        1225, 1226, 1227, 1228, 1229, 1230, 1231, 1232, 1233, 1234, 1235, 1236, 1237, 1238,
        1239, 1240, 1241, 1242, 1243, 1244, 1245, 1246, 1247, 1248, 1249, 1250, 1251, 1252,
        1253, 1254, 1255, 1256, 1257, 1258, 1259, 1260, 1261, 1262, 1263, 1264, 1265, 1266,
        1267, 1268, 1269, 1270, 1271,
        459, 468, 469,
        1033, 1034, 1035, 1036, 1037, 1038, 1039, 1040, 1041, 1042, 1043, 1044, 1045, 1046, 1047, 1048, 1049, 1050, 1051, 1052, 1053, 1054, 1055, 1056,
        1032,
        1031,
        1030,
        1028, 1029,
        1027,
        1026,
        1025,
        1024,
        1022,
        1021,
        1020,

        327, 414, 418, 546,
        1018,
        1017,
        1015,
        1014,
        15, 16, 17, 19, 20, 21, 31, 32, 37, 41, 47, 56, 58, 59,
        60, 92, 93, 94, 95, 97, 98, 99, 100, 101, 102, 103, 104, 105,
        107, 108, 109, 110, 111, 112, 113, 114, 116, 117, 118, 119, 120, 121,
        122, 123, 124, 125, 126, 127, 128, 129, 130, 131, 132, 133, 134, 135,
        136, 137, 138, 139, 140, 141, 142, 143, 144, 145, 146, 147, 148, 149,
        150, 151, 152, 153, 154, 156, 159, 160, 161, 162, 163, 165, 166, 167,
        168, 169, 170, 171, 172, 173, 174, 175, 176, 177, 182, 183, 184, 185,
        186, 187, 188, 190, 191, 192, 193, 194, 195, 196, 197, 198, 199, 200,
        201, 202, 203, 204, 205, 212, 215, 216, 217, 218, 219, 224, 225, 227,
        228, 230, 231, 233, 234, 236, 237, 238, 239, 240, 241, 242, 245, 246,
        248, 251, 255, 256, 257, 258, 260, 261, 262, 263, 265, 266, 268, 269,
        270, 271, 272, 273, 274, 275, 276, 277, 278, 279, 280, 281, 282, 283,
        284, 286, 287, 289, 290, 292, 293, 295, 296, 301, 303, 305, 308, 309,
        311, 314, 315, 321, 326, 329, 340, 341, 377, 379, 389, 390, 391, 394,
        395, 396, 397, 398, 401, 402, 403, 404, 405, 407, 408, 409, 410, 411,
        413, 417, 419, 422, 423, 424, 425, 427, 428, 429, 430, 431, 432,
        433, 434, 435, 436, 437, 438, 439, 440, 441, 442, 443, 444, 446, 447,
        448, 449, 450, 451, 452, 453, 454, 455, 456, 457, 458, 460, 461, 462,
        463, 464, 466, 467, 470, 471, 472, 473, 474, 475, 476, 477, 478, 479,
        480, 481, 482, 483, 484, 485, 486, 487, 488, 489, 490, 492, 493, 494,
        495, 496, 497, 498, 499, 504, 507, 508, 509, 510, 511, 512, 513, 514,
        515, 516, 517, 518, 519, 520, 521, 522, 523, 524, 525, 526, 527, 528,
        531, 533, 538, 541, 542, 544, 547, 548, 549, 550, 551, 552, 554, 555,
        556, 557, 560, 561, 562, 563, 564, 565, 567, 568, 569, 570, 571, 572,
        573, 574, 575, 576, 577, 578, 579, 580, 583, 584, 585, 586, 587, 588,
        591, 603, 608, 609, 611, 613, 614, 615, 616, 618, 619, 625, 626, 632,
        633, 636, 647, 648, 649, 654, 666, 677, 678, 681, 683, 684, 685, 686,
        687, 701, 702, 703, 704, 712, 733, 740, 741, 743, 744, 745, 746, 747,
        748, 750, 751, 752, 790, 801, 804, 816, 818, 825, 831, 835, 836, 843,
        845, 884, 885, 886, 887, 905, 906, 911, 913, 915, 916, 917, 918, 919,
        939, 943, 954, 958, 960, 971, 973, 986, 987, 993, 996, 997, 998, 999,
        1001, 1002, 1003, 1004, 1005, 1006, 1007, 1008, 1009, 1010, 1011, 1012, 1013
    )
}
