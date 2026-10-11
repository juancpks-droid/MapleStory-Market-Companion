Add-Type -AssemblyName System.Drawing
$folder=Join-Path $PSScriptRoot 'src/Market.App/Assets'
New-Item -ItemType Directory -Force $folder | Out-Null
$bitmap=[Drawing.Bitmap]::new(256,256)
$g=[Drawing.Graphics]::FromImage($bitmap)
$g.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([Drawing.Color]::Transparent)
$tile=[Drawing.Drawing2D.GraphicsPath]::new()
$tile.AddArc(8,8,64,64,180,90);$tile.AddArc(184,8,64,64,270,90);$tile.AddArc(184,184,64,64,0,90);$tile.AddArc(8,184,64,64,90,90);$tile.CloseFigure()
$fill=[Drawing.Drawing2D.LinearGradientBrush]::new([Drawing.Rectangle]::new(8,8,240,240),[Drawing.Color]::FromArgb(50,101,113),[Drawing.Color]::FromArgb(18,42,58),90)
$g.FillPath($fill,$tile)
$border=[Drawing.Pen]::new([Drawing.Color]::FromArgb(245,204,121),6);$g.DrawPath($border,$tile)
$points=@(@(123,32),@(140,73),@(157,60),@(156,94),@(188,78),@(181,112),@(205,115),@(167,150),@(173,169),@(135,161),@(139,207),@(121,210),@(122,162),@(84,176),@(86,152),@(46,120),@(70,112),@(60,82),@(94,96),@(90,61),@(108,77)) | ForEach-Object {[Drawing.PointF]::new($_[0],$_[1])}
$leaf=[Drawing.Drawing2D.GraphicsPath]::new();$leaf.AddPolygon([Drawing.PointF[]]$points)
$leafFill=[Drawing.Drawing2D.LinearGradientBrush]::new([Drawing.Rectangle]::new(46,30,165,185),[Drawing.Color]::FromArgb(255,185,71),[Drawing.Color]::FromArgb(213,63,38),90)
$g.FillPath($leafFill,$leaf);$edge=[Drawing.Pen]::new([Drawing.Color]::FromArgb(255,223,144),3);$g.DrawPath($edge,$leaf)
$vein=[Drawing.Pen]::new([Drawing.Color]::FromArgb(255,216,119),4);$vein.StartCap='Round';$vein.EndCap='Round';$g.DrawLine($vein,126,152,123,77);$g.DrawLine($vein,125,139,91,119);$g.DrawLine($vein,125,139,160,112)
$shadow=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(110,10,20,25));$g.FillEllipse($shadow,152,160,80,80)
$coin=[Drawing.Drawing2D.LinearGradientBrush]::new([Drawing.Rectangle]::new(149,149,76,76),[Drawing.Color]::FromArgb(255,240,159),[Drawing.Color]::FromArgb(230,150,29),90)
$g.FillEllipse($coin,149,149,76,76);$g.DrawEllipse($border,149,149,76,76)
$coinEdge=[Drawing.Pen]::new([Drawing.Color]::FromArgb(164,98,30),3);$g.DrawEllipse($coinEdge,158,158,58,58)
$mark=[Drawing.Pen]::new([Drawing.Color]::FromArgb(119,65,25),6);$mark.LineJoin='Round';$g.DrawLines($mark,[Drawing.PointF[]]@([Drawing.PointF]::new(172,202),[Drawing.PointF]::new(172,174),[Drawing.PointF]::new(187,191),[Drawing.PointF]::new(201,174),[Drawing.PointF]::new(201,202)))
$bitmap.Save((Join-Path $folder 'app-icon.png'),[Drawing.Imaging.ImageFormat]::Png)
$sizes=@(16,24,32,48,64,128,256);$frames=@()
foreach($size in $sizes){$small=[Drawing.Bitmap]::new($size,$size);$sg=[Drawing.Graphics]::FromImage($small);$sg.InterpolationMode='HighQualityBicubic';$sg.DrawImage($bitmap,0,0,$size,$size);$stream=[IO.MemoryStream]::new();$small.Save($stream,[Drawing.Imaging.ImageFormat]::Png);$frames+=,@($stream.ToArray());$sg.Dispose();$small.Dispose();$stream.Dispose()}
$output=[IO.File]::Create((Join-Path $folder 'app.ico'));$writer=[IO.BinaryWriter]::new($output);$writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$sizes.Count);$offset=6+16*$sizes.Count
for($i=0;$i -lt $sizes.Count;$i++){$dimension=if($sizes[$i] -eq 256){0}else{$sizes[$i]};$writer.Write([byte]$dimension);$writer.Write([byte]$dimension);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$frames[$i].Length);$writer.Write([uint32]$offset);$offset+=$frames[$i].Length}
foreach($frame in $frames){$writer.Write([byte[]]$frame)}
$writer.Dispose();$output.Dispose();$g.Dispose();$bitmap.Dispose()
foreach($resource in @($tile,$fill,$border,$leaf,$leafFill,$edge,$vein,$shadow,$coin,$coinEdge,$mark)){$resource.Dispose()}
