using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace RedlineDiagnostics.App
{
    /// <summary>Vector icons drawn with GDI+ so the app has no external image dependencies.</summary>
    public static class Icons
    {
        public static void Draw(Graphics g, string name, RectangleF r, Color color, float lineWidth = 1.8f)
        {
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(color, lineWidth) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var brush = new SolidBrush(color))
            {
                float x = r.X, y = r.Y, w = r.Width, h = r.Height;
                switch (name)
                {
                    case "home":
                        g.DrawLines(pen, new[] { P(x + w * .1f, y + h * .5f), P(x + w * .5f, y + h * .1f), P(x + w * .9f, y + h * .5f) });
                        g.DrawLines(pen, new[] { P(x + w * .2f, y + h * .45f), P(x + w * .2f, y + h * .9f), P(x + w * .8f, y + h * .9f), P(x + w * .8f, y + h * .45f) });
                        g.DrawRectangle(pen, x + w * .4f, y + h * .6f, w * .2f, h * .3f);
                        break;
                    case "diagnose":
                        g.DrawLines(pen, new[] { P(x, y + h * .5f), P(x + w * .2f, y + h * .5f), P(x + w * .32f, y + h * .15f), P(x + w * .48f, y + h * .85f), P(x + w * .62f, y + h * .3f), P(x + w * .72f, y + h * .5f), P(x + w, y + h * .5f) });
                        break;
                    case "livedata":
                        g.DrawLines(pen, new[] { P(x + w * .1f, y + h * .1f), P(x + w * .1f, y + h * .9f), P(x + w * .9f, y + h * .9f) });
                        g.FillRectangle(brush, x + w * .28f, y + h * .55f, w * .14f, h * .3f);
                        g.FillRectangle(brush, x + w * .5f, y + h * .3f, w * .14f, h * .55f);
                        g.FillRectangle(brush, x + w * .72f, y + h * .45f, w * .14f, h * .4f);
                        break;
                    case "vehicle":
                        using (var p = new GraphicsPath())
                        {
                            p.AddLines(new[] { P(x + w * .1f, y + h * .85f), P(x + w * .1f, y + h * .55f), P(x + w * .25f, y + h * .25f), P(x + w * .75f, y + h * .25f), P(x + w * .9f, y + h * .55f), P(x + w * .9f, y + h * .85f) });
                            g.DrawPath(pen, p);
                        }
                        g.DrawLine(pen, x + w * .1f, y + h * .55f, x + w * .9f, y + h * .55f);
                        g.FillEllipse(brush, x + w * .18f, y + h * .62f, w * .16f, h * .16f);
                        g.FillEllipse(brush, x + w * .66f, y + h * .62f, w * .16f, h * .16f);
                        g.DrawLine(pen, x + w * .18f, y + h * .85f, x + w * .18f, y + h * .95f);
                        g.DrawLine(pen, x + w * .82f, y + h * .85f, x + w * .82f, y + h * .95f);
                        break;
                    case "reports":
                        g.DrawLines(pen, new[] { P(x + w * .2f, y + h * .05f), P(x + w * .65f, y + h * .05f), P(x + w * .85f, y + h * .25f), P(x + w * .85f, y + h * .95f), P(x + w * .2f, y + h * .95f), P(x + w * .2f, y + h * .05f) });
                        g.DrawLine(pen, x + w * .35f, y + h * .45f, x + w * .7f, y + h * .45f);
                        g.DrawLine(pen, x + w * .35f, y + h * .62f, x + w * .7f, y + h * .62f);
                        g.DrawLine(pen, x + w * .35f, y + h * .79f, x + w * .6f, y + h * .79f);
                        break;
                    case "history":
                        g.DrawEllipse(pen, x + w * .08f, y + h * .08f, w * .84f, h * .84f);
                        g.DrawLine(pen, x + w * .5f, y + h * .25f, x + w * .5f, y + h * .52f);
                        g.DrawLine(pen, x + w * .5f, y + h * .52f, x + w * .7f, y + h * .62f);
                        break;
                    case "garage":
                        g.DrawLines(pen, new[] { P(x + w * .08f, y + h * .92f), P(x + w * .08f, y + h * .4f), P(x + w * .5f, y + h * .1f), P(x + w * .92f, y + h * .4f), P(x + w * .92f, y + h * .92f) });
                        g.DrawRectangle(pen, x + w * .25f, y + h * .5f, w * .5f, h * .42f);
                        g.DrawLine(pen, x + w * .25f, y + h * .64f, x + w * .75f, y + h * .64f);
                        g.DrawLine(pen, x + w * .25f, y + h * .78f, x + w * .75f, y + h * .78f);
                        break;
                    case "settings":
                        {
                            float cx = x + w / 2, cy = y + h / 2, ro = w * .46f, ri = w * .34f;
                            using (var p = new GraphicsPath())
                            {
                                var pts = new PointF[16];
                                for (int i = 0; i < 16; i++)
                                {
                                    double a = i * Math.PI / 8;
                                    float rad = (i % 2 == 0) ? ro : ri;
                                    pts[i] = P(cx + (float)Math.Cos(a) * rad, cy + (float)Math.Sin(a) * rad);
                                }
                                p.AddPolygon(pts);
                                g.DrawPath(pen, p);
                            }
                            g.DrawEllipse(pen, cx - w * .14f, cy - h * .14f, w * .28f, h * .28f);
                        }
                        break;
                    case "engine":
                        g.DrawLines(pen, new[] { P(x + w * .2f, y + h * .35f), P(x + w * .2f, y + h * .8f), P(x + w * .75f, y + h * .8f), P(x + w * .75f, y + h * .6f), P(x + w * .9f, y + h * .6f), P(x + w * .9f, y + h * .4f), P(x + w * .75f, y + h * .4f), P(x + w * .75f, y + h * .35f), P(x + w * .2f, y + h * .35f) });
                        g.DrawLine(pen, x + w * .35f, y + h * .35f, x + w * .35f, y + h * .2f);
                        g.DrawLine(pen, x + w * .25f, y + h * .2f, x + w * .55f, y + h * .2f);
                        g.DrawLine(pen, x + w * .08f, y + h * .5f, x + w * .2f, y + h * .5f);
                        g.DrawLine(pen, x + w * .08f, y + h * .4f, x + w * .08f, y + h * .65f);
                        break;
                    case "transmission":
                        g.DrawEllipse(pen, x + w * .1f, y + h * .1f, w * .8f, h * .8f);
                        g.DrawLine(pen, x + w * .5f, y + h * .25f, x + w * .5f, y + h * .75f);
                        g.DrawLine(pen, x + w * .3f, y + h * .45f, x + w * .7f, y + h * .45f);
                        g.DrawLine(pen, x + w * .3f, y + h * .25f, x + w * .3f, y + h * .65f);
                        g.DrawLine(pen, x + w * .7f, y + h * .25f, x + w * .7f, y + h * .65f);
                        break;
                    case "abs":
                        g.DrawEllipse(pen, x + w * .16f, y + h * .16f, w * .68f, h * .68f);
                        g.DrawArc(pen, x + w * .02f, y + h * .02f, w * .96f, h * .96f, 125, 110);
                        g.DrawArc(pen, x + w * .02f, y + h * .02f, w * .96f, h * .96f, 305, 110);
                        using (var f = new Font("Segoe UI", Math.Max(4f, w * 0.26f), FontStyle.Bold, GraphicsUnit.Pixel))
                        {
                            var sz = g.MeasureString("ABS", f);
                            g.DrawString("ABS", f, brush, x + w / 2 - sz.Width / 2, y + h / 2 - sz.Height / 2);
                        }
                        break;
                    case "airbag":
                        g.DrawEllipse(pen, x + w * .1f, y + h * .1f, w * .8f, h * .8f);
                        g.FillEllipse(brush, x + w * .38f, y + h * .22f, w * .24f, h * .24f);
                        g.DrawArc(pen, x + w * .25f, y + h * .5f, w * .5f, h * .5f, 180, 180);
                        break;
                    case "body":
                        g.DrawRectangle(pen, x + w * .15f, y + h * .3f, w * .7f, h * .55f);
                        g.DrawLine(pen, x + w * .35f, y + h * .3f, x + w * .35f, y + h * .15f);
                        g.DrawLine(pen, x + w * .65f, y + h * .3f, x + w * .65f, y + h * .15f);
                        g.DrawEllipse(pen, x + w * .42f, y + h * .48f, w * .16f, h * .16f);
                        break;
                    case "tpms":
                        g.DrawEllipse(pen, x + w * .1f, y + h * .1f, w * .8f, h * .8f);
                        g.DrawEllipse(pen, x + w * .3f, y + h * .3f, w * .4f, h * .4f);
                        g.DrawLine(pen, x + w * .5f, y + h * .18f, x + w * .5f, y + h * .3f);
                        break;
                    case "hvac":
                        {
                            float cx = x + w / 2, cy = y + h / 2;
                            for (int i = 0; i < 3; i++)
                            {
                                double a = i * Math.PI * 2 / 3 - Math.PI / 2;
                                g.DrawArc(pen, cx - w * .42f, cy - h * .42f, w * .84f, h * .84f, (float)(a * 180 / Math.PI) - 20, 40);
                                g.DrawLine(pen, cx, cy, cx + (float)Math.Cos(a) * w * .3f, cy + (float)Math.Sin(a) * h * .3f);
                            }
                            g.FillEllipse(brush, cx - w * .08f, cy - h * .08f, w * .16f, h * .16f);
                        }
                        break;
                    case "steering":
                        g.DrawEllipse(pen, x + w * .08f, y + h * .08f, w * .84f, h * .84f);
                        g.DrawEllipse(pen, x + w * .38f, y + h * .38f, w * .24f, h * .24f);
                        g.DrawLine(pen, x + w * .5f, y + h * .62f, x + w * .5f, y + h * .9f);
                        g.DrawLine(pen, x + w * .38f, y + h * .45f, x + w * .1f, y + h * .4f);
                        g.DrawLine(pen, x + w * .62f, y + h * .45f, x + w * .9f, y + h * .4f);
                        break;
                    case "cluster":
                        g.DrawArc(pen, x + w * .1f, y + h * .2f, w * .8f, h * .8f, 180, 180);
                        g.DrawLine(pen, x + w * .5f, y + h * .6f, x + w * .7f, y + h * .35f);
                        g.FillEllipse(brush, x + w * .45f, y + h * .55f, w * .1f, h * .1f);
                        break;
                    case "gateway":
                        g.DrawRectangle(pen, x + w * .3f, y + h * .3f, w * .4f, h * .4f);
                        g.DrawLine(pen, x + w * .5f, y + h * .05f, x + w * .5f, y + h * .3f);
                        g.DrawLine(pen, x + w * .5f, y + h * .7f, x + w * .5f, y + h * .95f);
                        g.DrawLine(pen, x + w * .05f, y + h * .5f, x + w * .3f, y + h * .5f);
                        g.DrawLine(pen, x + w * .7f, y + h * .5f, x + w * .95f, y + h * .5f);
                        break;
                    case "battery":
                        g.DrawRectangle(pen, x + w * .1f, y + h * .3f, w * .7f, h * .45f);
                        g.FillRectangle(brush, x + w * .8f, y + h * .42f, w * .1f, h * .2f);
                        g.FillRectangle(brush, x + w * .18f, y + h * .38f, w * .15f, h * .29f);
                        g.FillRectangle(brush, x + w * .38f, y + h * .38f, w * .15f, h * .29f);
                        break;
                    case "hybrid":
                        g.DrawEllipse(pen, x + w * .1f, y + h * .1f, w * .8f, h * .8f);
                        g.DrawLines(pen, new[] { P(x + w * .55f, y + h * .2f), P(x + w * .35f, y + h * .55f), P(x + w * .55f, y + h * .55f), P(x + w * .42f, y + h * .82f) });
                        break;
                    case "radar":
                        g.DrawArc(pen, x + w * .1f, y + h * .1f, w * .8f, h * .8f, 200, 140);
                        g.DrawArc(pen, x + w * .25f, y + h * .25f, w * .5f, h * .5f, 200, 140);
                        g.FillEllipse(brush, x + w * .44f, y + h * .6f, w * .12f, h * .12f);
                        break;
                    case "brake":
                        g.DrawEllipse(pen, x + w * .18f, y + h * .18f, w * .64f, h * .64f);
                        g.DrawArc(pen, x + w * .02f, y + h * .02f, w * .96f, h * .96f, 130, 100);
                        g.DrawArc(pen, x + w * .02f, y + h * .02f, w * .96f, h * .96f, 310, 100);
                        using (var f = new Font("Segoe UI", Math.Max(4f, w * 0.34f), FontStyle.Bold, GraphicsUnit.Pixel))
                        {
                            var sz = g.MeasureString("P", f);
                            g.DrawString("P", f, brush, x + w / 2 - sz.Width / 2, y + h / 2 - sz.Height / 2);
                        }
                        break;
                    case "key":
                        g.DrawEllipse(pen, x + w * .1f, y + h * .3f, w * .35f, h * .4f);
                        g.DrawLine(pen, x + w * .45f, y + h * .5f, x + w * .9f, y + h * .5f);
                        g.DrawLine(pen, x + w * .75f, y + h * .5f, x + w * .75f, y + h * .68f);
                        g.DrawLine(pen, x + w * .88f, y + h * .5f, x + w * .88f, y + h * .64f);
                        break;
                    case "media":
                        g.DrawRectangle(pen, x + w * .1f, y + h * .2f, w * .8f, h * .55f);
                        g.DrawLine(pen, x + w * .35f, y + h * .9f, x + w * .65f, y + h * .9f);
                        g.FillPolygon(brush, new[] { P(x + w * .42f, y + h * .35f), P(x + w * .42f, y + h * .6f), P(x + w * .62f, y + h * .47f) });
                        break;
                    case "telematics":
                        g.DrawArc(pen, x + w * .1f, y + h * .1f, w * .8f, h * .8f, 210, 120);
                        g.DrawArc(pen, x + w * .25f, y + h * .25f, w * .5f, h * .5f, 210, 120);
                        g.FillEllipse(brush, x + w * .44f, y + h * .58f, w * .12f, h * .12f);
                        g.DrawLine(pen, x + w * .5f, y + h * .7f, x + w * .5f, y + h * .92f);
                        break;
                    case "seat":
                        g.DrawLines(pen, new[] { P(x + w * .3f, y + h * .1f), P(x + w * .25f, y + h * .6f), P(x + w * .8f, y + h * .6f), P(x + w * .8f, y + h * .85f) });
                        g.DrawLine(pen, x + w * .25f, y + h * .6f, x + w * .25f, y + h * .85f);
                        break;
                    case "light":
                        g.DrawEllipse(pen, x + w * .15f, y + h * .2f, w * .45f, h * .6f);
                        g.DrawLine(pen, x + w * .7f, y + h * .3f, x + w * .92f, y + h * .3f);
                        g.DrawLine(pen, x + w * .7f, y + h * .5f, x + w * .92f, y + h * .5f);
                        g.DrawLine(pen, x + w * .7f, y + h * .7f, x + w * .92f, y + h * .7f);
                        break;
                    case "door":
                        g.DrawLines(pen, new[] { P(x + w * .15f, y + h * .9f), P(x + w * .15f, y + h * .45f), P(x + w * .45f, y + h * .1f), P(x + w * .85f, y + h * .1f), P(x + w * .85f, y + h * .9f), P(x + w * .15f, y + h * .9f) });
                        g.DrawLine(pen, x + w * .15f, y + h * .45f, x + w * .85f, y + h * .45f);
                        g.FillEllipse(brush, x + w * .62f, y + h * .58f, w * .1f, h * .1f);
                        break;
                    case "check":
                        g.DrawLines(pen, new[] { P(x + w * .2f, y + h * .52f), P(x + w * .42f, y + h * .74f), P(x + w * .8f, y + h * .3f) });
                        break;
                    case "warn":
                        g.DrawLine(pen, x + w * .5f, y + h * .2f, x + w * .5f, y + h * .58f);
                        g.FillEllipse(brush, x + w * .43f, y + h * .68f, w * .14f, h * .14f);
                        break;
                    case "cross":
                        g.DrawLine(pen, x + w * .25f, y + h * .25f, x + w * .75f, y + h * .75f);
                        g.DrawLine(pen, x + w * .75f, y + h * .25f, x + w * .25f, y + h * .75f);
                        break;
                    case "refresh":
                        g.DrawArc(pen, x + w * .15f, y + h * .15f, w * .7f, h * .7f, -60, 290);
                        g.FillPolygon(brush, new[] { P(x + w * .85f, y + h * .1f), P(x + w * .95f, y + h * .42f), P(x + w * .62f, y + h * .35f) });
                        break;
                    case "cube":
                        {
                            float cx = x + w / 2, cy = y + h / 2, s = w * .4f;
                            var top = new[] { P(cx, cy - s), P(cx + s * .87f, cy - s * .5f), P(cx, cy), P(cx - s * .87f, cy - s * .5f) };
                            g.DrawPolygon(pen, top);
                            g.DrawLines(pen, new[] { P(cx - s * .87f, cy - s * .5f), P(cx - s * .87f, cy + s * .5f), P(cx, cy + s), P(cx + s * .87f, cy + s * .5f), P(cx + s * .87f, cy - s * .5f) });
                            g.DrawLine(pen, cx, cy, cx, cy + s);
                        }
                        break;
                    case "wifi":
                        g.DrawArc(pen, x + w * .05f, y + h * .15f, w * .9f, h * .9f, 220, 100);
                        g.DrawArc(pen, x + w * .22f, y + h * .35f, w * .56f, h * .56f, 220, 100);
                        g.FillEllipse(brush, x + w * .44f, y + h * .72f, w * .12f, h * .12f);
                        break;
                    case "bluetooth":
                        g.DrawLines(pen, new[] { P(x + w * .25f, y + h * .3f), P(x + w * .75f, y + h * .7f), P(x + w * .5f, y + h * .92f), P(x + w * .5f, y + h * .08f), P(x + w * .75f, y + h * .3f), P(x + w * .25f, y + h * .7f) });
                        break;
                    case "batteryLevel":
                        g.DrawRectangle(pen, x + w * .1f, y + h * .25f, w * .7f, h * .5f);
                        g.FillRectangle(brush, x + w * .82f, y + h * .4f, w * .08f, h * .2f);
                        g.FillRectangle(brush, x + w * .18f, y + h * .33f, w * .5f, h * .34f);
                        break;
                    case "back":
                        g.DrawLines(pen, new[] { P(x + w * .65f, y + h * .15f), P(x + w * .3f, y + h * .5f), P(x + w * .65f, y + h * .85f) });
                        break;
                    case "forward":
                        g.DrawLines(pen, new[] { P(x + w * .35f, y + h * .15f), P(x + w * .7f, y + h * .5f), P(x + w * .35f, y + h * .85f) });
                        break;
                    case "up":
                        g.DrawLines(pen, new[] { P(x + w * .15f, y + h * .65f), P(x + w * .5f, y + h * .3f), P(x + w * .85f, y + h * .65f) });
                        break;
                    case "down":
                        g.DrawLines(pen, new[] { P(x + w * .15f, y + h * .35f), P(x + w * .5f, y + h * .7f), P(x + w * .85f, y + h * .35f) });
                        break;
                    case "plug":
                        g.DrawRectangle(pen, x + w * .25f, y + h * .35f, w * .5f, h * .35f);
                        g.DrawLine(pen, x + w * .37f, y + h * .35f, x + w * .37f, y + h * .12f);
                        g.DrawLine(pen, x + w * .63f, y + h * .35f, x + w * .63f, y + h * .12f);
                        g.DrawLine(pen, x + w * .5f, y + h * .7f, x + w * .5f, y + h * .92f);
                        break;
                    case "scan":
                        g.DrawEllipse(pen, x + w * .15f, y + h * .15f, w * .7f, h * .7f);
                        g.DrawArc(pen, x + w * .3f, y + h * .3f, w * .4f, h * .4f, 200, 250);
                        g.FillEllipse(brush, x + w * .44f, y + h * .44f, w * .12f, h * .12f);
                        break;
                    case "erase":
                        g.DrawLine(pen, x + w * .2f, y + h * .3f, x + w * .8f, y + h * .3f);
                        g.DrawRectangle(pen, x + w * .28f, y + h * .3f, w * .44f, h * .55f);
                        g.DrawLine(pen, x + w * .4f, y + h * .2f, x + w * .6f, y + h * .2f);
                        break;
                    case "target":
                        g.DrawEllipse(pen, x + w * .12f, y + h * .12f, w * .76f, h * .76f);
                        g.DrawEllipse(pen, x + w * .32f, y + h * .32f, w * .36f, h * .36f);
                        g.DrawLine(pen, x + w * .5f, y + h * .02f, x + w * .5f, y + h * .22f);
                        g.DrawLine(pen, x + w * .5f, y + h * .78f, x + w * .5f, y + h * .98f);
                        g.DrawLine(pen, x + w * .02f, y + h * .5f, x + w * .22f, y + h * .5f);
                        g.DrawLine(pen, x + w * .78f, y + h * .5f, x + w * .98f, y + h * .5f);
                        break;
                    case "car-top":
                        using (var p = new GraphicsPath())
                        {
                            p.AddClosedCurve(new[] { P(x + w * .5f, y + h * .05f), P(x + w * .8f, y + h * .2f), P(x + w * .85f, y + h * .5f), P(x + w * .8f, y + h * .85f), P(x + w * .5f, y + h * .95f), P(x + w * .2f, y + h * .85f), P(x + w * .15f, y + h * .5f), P(x + w * .2f, y + h * .2f) }, 0.6f);
                            g.DrawPath(pen, p);
                        }
                        g.DrawLine(pen, x + w * .25f, y + h * .35f, x + w * .75f, y + h * .35f);
                        g.DrawLine(pen, x + w * .25f, y + h * .7f, x + w * .75f, y + h * .7f);
                        break;
                    case "plus":
                        g.DrawLine(pen, x + w * .5f, y + h * .2f, x + w * .5f, y + h * .8f);
                        g.DrawLine(pen, x + w * .2f, y + h * .5f, x + w * .8f, y + h * .5f);
                        break;
                    case "trash":
                        g.DrawLine(pen, x + w * .2f, y + h * .25f, x + w * .8f, y + h * .25f);
                        g.DrawLines(pen, new[] { P(x + w * .28f, y + h * .25f), P(x + w * .32f, y + h * .9f), P(x + w * .68f, y + h * .9f), P(x + w * .72f, y + h * .25f) });
                        g.DrawLine(pen, x + w * .4f, y + h * .15f, x + w * .6f, y + h * .15f);
                        break;
                    case "folder":
                        g.DrawLines(pen, new[] { P(x + w * .1f, y + h * .8f), P(x + w * .1f, y + h * .25f), P(x + w * .4f, y + h * .25f), P(x + w * .48f, y + h * .35f), P(x + w * .9f, y + h * .35f), P(x + w * .9f, y + h * .8f), P(x + w * .1f, y + h * .8f) });
                        break;
                    case "save":
                        g.DrawRectangle(pen, x + w * .15f, y + h * .15f, w * .7f, h * .7f);
                        g.DrawRectangle(pen, x + w * .32f, y + h * .15f, w * .36f, h * .22f);
                        g.DrawRectangle(pen, x + w * .28f, y + h * .55f, w * .44f, h * .3f);
                        break;
                    default:
                        g.DrawEllipse(pen, x + w * .2f, y + h * .2f, w * .6f, h * .6f);
                        break;
                }
            }
            g.SmoothingMode = old;
        }

        /// <summary>Icon inside a rounded, tinted square (used for module cards and callouts).</summary>
        public static void Badge(Graphics g, string icon, RectangleF r, Color color, float radius = 6f)
        {
            Theme.FillRounded(g, r, radius, Theme.WithAlpha(color, 34));
            Theme.DrawRounded(g, r, radius, Theme.WithAlpha(color, 120));
            var inner = RectangleF.Inflate(r, -r.Width * 0.2f, -r.Height * 0.2f);
            Draw(g, icon, inner, color, 1.6f);
        }

        /// <summary>Filled status circle with a check / exclamation / hourglass glyph.</summary>
        public static void StatusDot(Graphics g, RectangleF r, Diagnostics.ModuleStatus status)
        {
            var c = Theme.StatusColor(status);
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(c)) g.FillEllipse(b, r);
            var inner = RectangleF.Inflate(r, -r.Width * 0.12f, -r.Height * 0.12f);
            switch (status)
            {
                case Diagnostics.ModuleStatus.Passed: Draw(g, "check", inner, Color.White, 2f); break;
                case Diagnostics.ModuleStatus.Fault:
                case Diagnostics.ModuleStatus.Warning: Draw(g, "warn", inner, Color.White, 2f); break;
                case Diagnostics.ModuleStatus.NoResponse: Draw(g, "cross", inner, Color.White, 2f); break;
                case Diagnostics.ModuleStatus.Scanning:
                    using (var pen = new Pen(Color.White, 1.6f))
                        g.DrawArc(pen, RectangleF.Inflate(r, -r.Width * 0.28f, -r.Height * 0.28f), -90, 270);
                    break;
                default:
                    using (var pen = new Pen(Color.White, 1.6f))
                    {
                        float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
                        g.DrawEllipse(pen, cx - r.Width * .2f, cy - r.Height * .28f, r.Width * .4f, r.Height * .3f);
                        g.DrawArc(pen, cx - r.Width * .28f, cy - r.Height * .05f, r.Width * .56f, r.Height * .5f, 180, 180);
                    }
                    break;
            }
            g.SmoothingMode = old;
        }

        public static void Logo(Graphics g, RectangleF r, Color red)
        {
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float h = r.Height, x = r.X, y = r.Y;
            using (var path = new GraphicsPath())
            {
                path.AddLines(new[] {
                    P(x + h * .05f, y + h * .95f), P(x + h * .25f, y + h * .05f), P(x + h * .75f, y + h * .05f),
                    P(x + h * .95f, y + h * .3f), P(x + h * .75f, y + h * .55f), P(x + h * .5f, y + h * .55f),
                    P(x + h * .85f, y + h * .95f), P(x + h * .6f, y + h * .95f), P(x + h * .32f, y + h * .6f),
                    P(x + h * .27f, y + h * .95f) });
                path.CloseFigure();
                using (var b = new LinearGradientBrush(new RectangleF(x - 1, y - 1, h + 2, h + 2), Theme.Lerp(red, Color.White, 0.2f), Theme.RedDark, 60f))
                    g.FillPath(b, path);
            }
            using (var pen = new Pen(Theme.WithAlpha(red, 180), 2f))
                g.DrawLine(pen, x - h * .15f, y + h * .75f, x + h * .12f, y + h * .75f);
            using (var pen = new Pen(Theme.WithAlpha(red, 100), 2f))
                g.DrawLine(pen, x - h * .3f, y + h * .88f, x + h * .06f, y + h * .88f);
            g.SmoothingMode = old;
        }

        private static PointF P(float x, float y) => new PointF(x, y);
    }
}
