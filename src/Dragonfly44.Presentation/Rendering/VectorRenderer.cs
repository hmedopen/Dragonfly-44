using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Windows.UI;

namespace Dragonfly44.Presentation.Rendering;

public class VectorRenderer
{
    public void DrawGrid(
        CanvasDrawingSession session,
        float width,
        float height,
        float stepSize,
        Color lineColor,
        float strokeWidth = 1.0f)
    {
        for (float x = 0; x <= width; x += stepSize)
        {
            session.DrawLine(x, 0, x, height, lineColor, strokeWidth);
        }

        for (float y = 0; y <= height; y += stepSize)
        {
            session.DrawLine(0, y, width, y, lineColor, strokeWidth);
        }
    }

    public void DrawCubicBezier(
        CanvasDrawingSession session,
        Vector2 start,
        Vector2 control1,
        Vector2 control2,
        Vector2 end,
        Color color,
        float strokeWidth = 2.0f)
    {
        using var pathBuilder = new CanvasPathBuilder(session);
        pathBuilder.BeginFigure(start);
        pathBuilder.AddCubicBezier(control1, control2, end);
        pathBuilder.EndFigure(CanvasFigureLoop.Open);

        using var geometry = CanvasGeometry.CreatePath(pathBuilder);
        session.DrawGeometry(geometry, color, strokeWidth);
    }

    public void DrawRoundedCard(
        CanvasDrawingSession session,
        float x,
        float y,
        float width,
        float height,
        float radius,
        Color fillColor,
        Color borderColor,
        float strokeWidth = 1.0f)
    {
        session.FillRoundedRectangle(x, y, width, height, radius, radius, fillColor);
        if (strokeWidth > 0)
        {
            session.DrawRoundedRectangle(x, y, width, height, radius, radius, borderColor, strokeWidth);
        }
    }

    public void DrawTechnicalCrosshair(
        CanvasDrawingSession session,
        Vector2 center,
        float size,
        Color color,
        float strokeWidth = 1.0f)
    {
        session.DrawLine(center.X - size, center.Y, center.X + size, center.Y, color, strokeWidth);
        session.DrawLine(center.X, center.Y - size, center.X, center.Y + size, color, strokeWidth);
        session.DrawCircle(center, size * 0.5f, color, strokeWidth);
    }
}
