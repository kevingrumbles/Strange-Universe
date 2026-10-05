using System.Collections.Generic;

namespace Strange_Universe.Tests;

internal static class StarSystemGoldenData
{
    public static readonly Dictionary<string, string> Expected = new()
    {
        { "golden-b|Beta", @"P5 A33 S2 R25568.879 Belt7140.9473-12414.663 BG610 C3 Stars[Galae Gate(2511.2322,0)3;Noroiar(-2511.2322,-0.0002195389)0] P0(-3976.587,-3642.1125)r136.40538 Ast[(-10365.088,-2251.1702);(6632.4,9475.585);(6273.011,7934.066)] BGS[(1409.0089,97.75168)1;(809.03143,22.428328)1;(605.87714,914.16504)0] AstSum16889.157917022705 Nodes[golden-b_Beta(-3,7);golden-b_Sol(-5,5);golden-b_Noran Haven(-6,-2);golden-b_Taliaia(0,-5)] Conn[golden-b_Noran Haven;golden-b_Sol;golden-b_Taliaia]" },
        { "golden-a|", @"P8 A100 S1 R16802.07 Belt5924.98-9298.619 BG715 C4 Stars[Araia Haven(0,0)0] P0(353.60614,-2243.192)r147.38559 Ast[(8748.083,51.167282);(-3612.9224,-5347.4526);(-4901.8213,-6872.921)] BGS[(1612.2867,572.0228)0;(244.2435,55.569496)0;(42.811718,490.69168)0] AstSum-116048.08596038818 Nodes[golden-a_Sol(0,0);golden-a_Valia Reach(-3,-9);golden-a_Newaor Prime(8,2);golden-a_Westi Point(6,-2);golden-a_Daloel Haven(-2,8)] Conn[golden-a_Daloel Haven;golden-a_Newaor Prime;golden-a_Valia Reach;golden-a_Westi Point]" },
        { "golden-d|Delta", @"P4 A53 S2 R25875.617 Belt9283.858-13048.009 BG441 C2 Stars[Galelan Haven(3357.2134,0)1;Dalis Prime(-3357.2134,-0.0002934969)0] P0(5628.928,4800.462)r226.56543 Ast[(-10765.227,7115.655);(-7431.337,-8036.237);(-10698.753,-2303.6045)] BGS[(472.87222,1079.4203)1;(690.20917,691.2684)0;(166.53188,462.05026)0] AstSum-47964.11403155327 Nodes[golden-d_Delta(0,9);golden-d_Sol(-2,17);golden-d_Farorelia(9,12)] Conn[golden-d_Farorelia;golden-d_Sol]" },
        { "golden-c|Gamma", @"P0 A27 S2 R23690.957 Belt8603.195-12172.115 BG480 C2 Stars[Galanear(3095.392,0)0;Taliau Point(-3095.392,-0.00027060776)1] Ast[(-4475.2383,8684.017);(8645.092,-6427.5703);(10263.632,3415.3032)] BGS[(872.21735,734.6755)0;(1902.738,263.87668)1;(904.8225,636.58997)0] AstSum-54706.55340576172 Nodes[golden-c_Gamma(12,-4);golden-c_Sol(3,-10);golden-c_Helis Gate(10,2)] Conn[golden-c_Helis Gate;golden-c_Sol]" },
        { "golden-a|Alpha", @"P1 A48 S2 R27168.65 Belt8640.544-14111.973 BG537 C3 Stars[Helisisia(3043.775,0)0;Daleor Haven(-3043.775,-0.00026609525)0] P0(-12963.785,15720.736)r139.94226 Ast[(-6436.982,7118.0215);(13061.376,-5190.177);(-4671.6777,7652.1074)] BGS[(665.65283,183.13483)1;(1413.0598,38.402065)0;(1603.2308,766.2145)0] AstSum-2039.0309753417969 Nodes[golden-a_Alpha(5,5);golden-a_Sol(11,7);golden-a_Valia Reach(-1,2);golden-a_Newaor Prime(7,5)] Conn[golden-a_Newaor Prime;golden-a_Sol;golden-a_Valia Reach]" },
        { "seed-1|", @"P8 A100 S1 R14308.775 Belt4792.5996-6790.8345 BG599 C4 Stars[Dalaon(0,0)2] P0(-633.93536,-1500.004)r117.827515 Ast[(5679.988,-3526.2783);(-4549.3013,4602.4604);(2444.1653,-5187.7354)] BGS[(739.9764,682.58997)0;(1414.4409,89.061615)1;(43.40952,1037.5016)0] AstSum-43695.39197921753 Nodes[seed-1_Sol(0,0);seed-1_Eloar(-4,-2);seed-1_Westiaiaon(6,-4);seed-1_Elisanus(9,-3);seed-1_Beleia Prime(4,-2)] Conn[seed-1_Beleia Prime;seed-1_Elisanus;seed-1_Eloar;seed-1_Westiaiaon]" },
    };
}
