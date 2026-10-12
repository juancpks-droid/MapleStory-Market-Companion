Add-Type -AssemblyName System.Drawing
$folder=Join-Path $PSScriptRoot 'src/Market.App/Assets'
New-Item -ItemType Directory -Force $folder | Out-Null
$bitmap=[Drawing.Bitmap]::new(256,256)
$g=[Drawing.Graphics]::FromImage($bitmap)
$g.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([Drawing.Color]::Transparent)
$tile=[Drawing.Drawing2D.GraphicsPath]::new()
$tile.AddArc(8,8,64,64,180,90);$tile.AddArc(184,8,64,64,270,90);$tile.AddArc(184,184,64,64,0,90);$tile.AddArc(8,184,64,64,90,90);$tile.CloseFigure()
$fill=[Drawing.Drawing2D.LinearGradientBrush]::new([Drawing.Rectangle]::new(8,8,240,240),[Drawing.Color]::FromArgb(43,48,54),[Drawing.Color]::FromArgb(17,22,28),90)
$g.FillPath($fill,$tile)
$border=[Drawing.Pen]::new([Drawing.Color]::FromArgb(245,204,121),6);$g.DrawPath($border,$tile)
$points=@(@(128,35),@(147,80),@(170,61),@(167,100),@(205,93),@(186,125),@(202,141),@(144,154),@(138,196),@(122,196),@(125,154),@(64,141),@(80,122),@(58,90),@(96,100),@(90,61),@(112,80)) | ForEach-Object {[Drawing.PointF]::new($_[0],$_[1])}
$leaf=[Drawing.Drawing2D.GraphicsPath]::new();$leaf.AddPolygon([Drawing.PointF[]]$points)
$leafFill=[Drawing.Drawing2D.LinearGradientBrush]::new([Drawing.Rectangle]::new(46,30,165,185),[Drawing.Color]::FromArgb(255,209,101),[Drawing.Color]::FromArgb(226,100,37),90)
$g.FillPath($leafFill,$leaf);$edge=[Drawing.Pen]::new([Drawing.Color]::FromArgb(255,223,144),3);$g.DrawPath($edge,$leaf)
$vein=[Drawing.Pen]::new([Drawing.Color]::FromArgb(255,216,119),4);$vein.StartCap='Round';$vein.EndCap='Round';$g.DrawLine($vein,126,152,123,77);$g.DrawLine($vein,125,139,91,119);$g.DrawLine($vein,125,139,160,112)
$bitmap.Save((Join-Path $folder 'app-icon.png'),[Drawing.Imaging.ImageFormat]::Png)
$sizes=@(16,24,32,48,64,128,256);$frames=@()
foreach($size in $sizes){$small=[Drawing.Bitmap]::new($size,$size);$sg=[Drawing.Graphics]::FromImage($small);$sg.InterpolationMode='HighQualityBicubic';$sg.DrawImage($bitmap,0,0,$size,$size);$stream=[IO.MemoryStream]::new();$small.Save($stream,[Drawing.Imaging.ImageFormat]::Png);$frames+=,@($stream.ToArray());$sg.Dispose();$small.Dispose();$stream.Dispose()}
$output=[IO.File]::Create((Join-Path $folder 'app.ico'));$writer=[IO.BinaryWriter]::new($output);$writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$sizes.Count);$offset=6+16*$sizes.Count
for($i=0;$i -lt $sizes.Count;$i++){$dimension=if($sizes[$i] -eq 256){0}else{$sizes[$i]};$writer.Write([byte]$dimension);$writer.Write([byte]$dimension);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$frames[$i].Length);$writer.Write([uint32]$offset);$offset+=$frames[$i].Length}
foreach($frame in $frames){$writer.Write([byte[]]$frame)}
$writer.Dispose();$output.Dispose();$g.Dispose();$bitmap.Dispose()
foreach($resource in @($tile,$fill,$border,$leaf,$leafFill,$edge,$vein)){$resource.Dispose()}
