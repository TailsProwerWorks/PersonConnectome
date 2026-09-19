# Original side-profile artwork; no extracted game textures or external art.
[CmdletBinding()]
param([string] $OutputDirectory = (Join-Path $PSScriptRoot '..\..\assets\fly'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing.Common, System.Drawing.Primitives, System.Private.Windows.GdiPlus, System.Private.Windows.Core -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
public static class FlySpriteArtwork
{
    static Color C(string hex) { return ColorTranslator.FromHtml(hex); }
    static void Oval(Graphics g, string color, float x, float y, float w, float h)
    { using(var b = new SolidBrush(C(color))) g.FillEllipse(b,x,y,w,h); }
    static void Line(Graphics g, string color, float width, params float[] xy)
    {
        var points = new PointF[xy.Length/2];
        for(int i=0;i<points.Length;i++) points[i]=new PointF(xy[i*2],xy[i*2+1]);
        using(var p=new Pen(C(color),width)) { p.StartCap=p.EndCap=LineCap.Round; g.DrawLines(p,points); }
    }
    static void Shape(Graphics g, string color, params float[] xy)
    {
        var points = new PointF[xy.Length/2];
        for(int i=0;i<points.Length;i++) points[i]=new PointF(xy[i*2],xy[i*2+1]);
        using(var b=new SolidBrush(C(color))) g.FillClosedCurve(b,points,FillMode.Winding,.3f);
    }
    static Bitmap Draw(string part, int w, int h)
    {
        var b=new Bitmap(w,h,PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(b))
        {
            g.SmoothingMode=SmoothingMode.AntiAlias;
            g.ScaleTransform(w/100f,h/100f);
            if(part=="head")
            {
                Shape(g,"#241b19", 28,21, 61,13, 84,30, 90,64, 67,87, 30,77, 21,54);
                Shape(g,"#8c6942", 29,26, 58,17, 76,30, 82,63, 62,80, 32,71, 25,51);
                Oval(g,"#351515",26,26,48,51); Oval(g,"#8f201d",27,27,44,47);
                Oval(g,"#c3402b",29,29,33,39); Oval(g,"#e7683b",33,32,16,21);
                for(int y=35;y<69;y+=6) for(int x=32+(y%2)*3;x<64;x+=6)
                    if(Math.Pow((x-48)/19.0,2)+Math.Pow((y-50)/21.0,2)<1) Oval(g,"#73251f",x,y,2.2f,2.2f);
                Line(g,"#35271d",3, 27,35,17,28,8,30);
                Line(g,"#493525",2, 18,28,13,14,5,8);
                Line(g,"#493525",1.2f, 13,18,5,17); Line(g,"#493525",1.2f, 11,12,13,4);
                Line(g,"#422c20",4, 32,68,20,81,14,85); Oval(g,"#61422a",9,80,9,7);
                Line(g,"#36291e",1.4f, 65,19,68,8); Line(g,"#36291e",1.4f, 78,26,87,16);
            }
            if(part=="thorax")
            {
                Shape(g,"#29221c", 6,49,13,27,34,11,60,10,83,28,95,55,83,77,52,88,21,76);
                Shape(g,"#80653e", 11,47,19,29,37,15,61,15,80,30,87,52,77,72,48,81,22,69);
                Shape(g,"#ac8d59", 18,42,32,24,51,18,66,26,61,42,34,56);
                Line(g,"#4b412a",3, 31,26,41,24,63,44,71,64);
                Line(g,"#5a4a2f",2.5f, 47,20,61,27,76,48);
                Shape(g,"#5a432c", 18,58,45,61,70,75,46,81,24,72);
                for(int i=0;i<7;i++) { float x=23+i*9; Line(g,"#2c281e",1.4f,x,24+(i%3)*3,x-4,7+(i%3)*4); }
                for(int i=0;i<5;i++) Line(g,"#3f3022",1.2f,27+i*11,72,25+i*11,84);
            }
            if(part=="abdomen")
            {
                Shape(g,"#2a221c", 4,40,20,23,44,17,66,25,84,41,97,63,83,77,57,84,28,77,8,62);
                Shape(g,"#b79354", 7,42,22,28,44,22,64,29,82,45,91,62,80,72,56,78,30,72,11,59);
                Shape(g,"#d1af70", 15,40,33,28,47,27,58,34,41,44,22,50);
                Shape(g,"#503c29", 22,28,28,26,35,46,35,74,28,72,28,48);
                Shape(g,"#453526", 42,22,49,23,57,47,57,78,48,77,50,50);
                Shape(g,"#3c2d22", 63,29,70,33,78,52,76,74,66,78,69,53);
                Shape(g,"#392b22", 83,45,89,54,95,63,83,75,79,71,86,60);
                for(int i=0;i<8;i++) Line(g,"#403024",.8f,22+i*8,73+(i%2)*3,23+i*8,82+(i%2)*3);
            }
            if(part=="wing")
            {
                Shape(g,"#879792", 3,65,19,40,50,18,79,13,96,30,90,55,62,73,28,82,7,78);
                Shape(g,"#d5ddd1", 5,65,21,43,50,22,79,17,92,31,87,52,60,69,27,77,8,74);
                Line(g,"#7a8980",1.2f,6,69,35,52,66,33,87,29);
                Line(g,"#879387",1,8,72,40,62,66,55,86,43);
                Line(g,"#8a968b",1,13,73,40,72,69,63,84,53);
                Line(g,"#748478",1,35,52,40,62,40,72);
                Line(g,"#8b978b",1,66,33,66,55,69,63);
                for(int y=0;y<h;y++) for(int x=0;x<w;x++) { var c=b.GetPixel(x,y); if(c.A>0) b.SetPixel(x,y,Color.FromArgb(c.A*155/255,c)); }
            }
            if(part=="femur" || part=="tibia")
            {
                bool lower=part=="tibia";
                Shape(g,"#2d241a", 43,2,64,5,68,21,57,66,54,91,lower?88:50,97,lower?58:35,99,34,88,35,52,30,15);
                Line(g,"#927143",lower?10:18,47,9,49,30,43,70,43,88);
                Line(g,"#bea071",lower?3:5,44,11,43,33,39,62);
                Oval(g,"#53412a",34,1,30,9);
                for(int i=0;i<4;i++) Line(g,"#34291b",3,55,25+i*15,77,20+i*15);
                if(lower) { Line(g,"#493520",6,44,89,63,96,90,95); Line(g,"#261f17",3,87,95,92,90); }
            }
        }
        return b;
    }
    public static void Build(string directory)
    {
        Directory.CreateDirectory(directory);
        string[] names={"head","thorax","abdomen","wing","femur","tibia"};
        int[] widths={64,76,112,152,14,18}, heights={64,64,64,52,48,58};
        for(int i=0;i<names.Length;i++) using(var b=Draw(names[i],widths[i],heights[i]))
        {
            b.Save(Path.Combine(directory,names[i]+".png"),ImageFormat.Png);
            foreach(string layer in new[]{"flesh","bone"}) using(var map=new Bitmap(b.Width,b.Height))
            {
                for(int y=0;y<b.Height;y++) for(int x=0;x<b.Width;x++)
                { var c=b.GetPixel(x,y); int grain=(x*17+y*31)%19; map.SetPixel(x,y,layer=="flesh"?Color.FromArgb(c.A,114+grain,25+grain/2,26):Color.FromArgb(c.A,184+grain,167+grain,125+grain)); }
                map.Save(Path.Combine(directory,names[i]+"-"+layer+".png"),ImageFormat.Png);
            }
        }
    }
}
'@
[FlySpriteArtwork]::Build([IO.Path]::GetFullPath($OutputDirectory))
Write-Host "Built fly skin, flesh, and bone sprites in $OutputDirectory"

