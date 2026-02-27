using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using RecipeStudio.Desktop.Graphics3D;
using RecipeStudio.Desktop.Models;
using RecipeStudio.Desktop.Services;
using Silk.NET.OpenGL;

namespace RecipeStudio.Desktop.Controls;

public sealed unsafe class Simulation3DControl : OpenGlControlBase
{
    public static readonly StyledProperty<IList<RecipePoint>?> PointsProperty = AvaloniaProperty.Register<Simulation3DControl, IList<RecipePoint>?>(nameof(Points));
    public static readonly StyledProperty<AppSettings?> SettingsProperty = AvaloniaProperty.Register<Simulation3DControl, AppSettings?>(nameof(Settings));
    public static readonly StyledProperty<bool> ShowGridProperty = AvaloniaProperty.Register<Simulation3DControl, bool>(nameof(ShowGrid), true);
    public static readonly StyledProperty<bool> ShowPairLinksProperty = AvaloniaProperty.Register<Simulation3DControl, bool>(nameof(ShowPairLinks), true);
    public static readonly StyledProperty<double> ToolXRawProperty = AvaloniaProperty.Register<Simulation3DControl, double>(nameof(ToolXRaw));
    public static readonly StyledProperty<double> ToolYRawProperty = AvaloniaProperty.Register<Simulation3DControl, double>(nameof(ToolYRaw));
    public static readonly StyledProperty<double> ToolZRawProperty = AvaloniaProperty.Register<Simulation3DControl, double>(nameof(ToolZRaw));
    public static readonly StyledProperty<double> CurrentAlfaProperty = AvaloniaProperty.Register<Simulation3DControl, double>(nameof(CurrentAlfa));
    public static readonly StyledProperty<double> CurrentBettaProperty = AvaloniaProperty.Register<Simulation3DControl, double>(nameof(CurrentBetta));
    public static readonly StyledProperty<bool> ShowBlueprintsProperty = AvaloniaProperty.Register<Simulation3DControl, bool>(nameof(ShowBlueprints), true);
    public static readonly StyledProperty<double> BlueprintOpacityProperty = AvaloniaProperty.Register<Simulation3DControl, double>(nameof(BlueprintOpacity), 0.3);
    public static readonly StyledProperty<Vector3> BlueprintOffsetProperty = AvaloniaProperty.Register<Simulation3DControl, Vector3>(nameof(BlueprintOffset), new Vector3(0, -120, 0));
    public static readonly StyledProperty<double> BlueprintScaleProperty = AvaloniaProperty.Register<Simulation3DControl, double>(nameof(BlueprintScale), 1.0);

    private GL? _gl;
    private ShaderProgram? _meshShader;
    private ShaderProgram? _lineShader;
    private ShaderProgram? _texShader;
    private CameraOrbit _camera = new();

    private Mesh? _partMesh;
    private Mesh? _nozzleMesh;
    private Mesh? _jetConeMesh;
    private Mesh? _quadMesh;
    private uint _trajectoryVao;
    private uint _trajectoryVbo;
    private int _trajectoryCount;
    private uint _targetTrajectoryVao;
    private uint _targetTrajectoryVbo;
    private int _targetTrajectoryCount;
    private uint _gridVao;
    private uint _gridVbo;
    private int _gridCount;

    private uint _blueprintPartTex;
    private uint _dynamicLineVao;
    private uint _dynamicLineVbo;
    private uint _blueprintManipulatorTex;
    private uint _blueprintNozzleTex;

    private bool _isPanning;
    private Point _last;
    private bool _failed;
    private bool _glInitialized;
    private bool _hasRenderedFrame;
    private string? _failureDetails;
    private bool _geometryDirty = true;
    private readonly AppLogger _logger = new();
    private bool _loggedRenderSuccess;
    private bool _loggerConfigured;

    private List<Vector3> _toolPath = new();
    private List<Vector3> _targetPath = new();

    public IList<RecipePoint>? Points { get => GetValue(PointsProperty); set => SetValue(PointsProperty, value); }
    public AppSettings? Settings { get => GetValue(SettingsProperty); set => SetValue(SettingsProperty, value); }
    public bool ShowGrid { get => GetValue(ShowGridProperty); set => SetValue(ShowGridProperty, value); }
    public bool ShowPairLinks { get => GetValue(ShowPairLinksProperty); set => SetValue(ShowPairLinksProperty, value); }
    public double ToolXRaw { get => GetValue(ToolXRawProperty); set => SetValue(ToolXRawProperty, value); }
    public double ToolYRaw { get => GetValue(ToolYRawProperty); set => SetValue(ToolYRawProperty, value); }
    public double ToolZRaw { get => GetValue(ToolZRawProperty); set => SetValue(ToolZRawProperty, value); }
    public double CurrentAlfa { get => GetValue(CurrentAlfaProperty); set => SetValue(CurrentAlfaProperty, value); }
    public double CurrentBetta { get => GetValue(CurrentBettaProperty); set => SetValue(CurrentBettaProperty, value); }
    public bool ShowBlueprints { get => GetValue(ShowBlueprintsProperty); set => SetValue(ShowBlueprintsProperty, value); }
    public double BlueprintOpacity { get => GetValue(BlueprintOpacityProperty); set => SetValue(BlueprintOpacityProperty, value); }
    public Vector3 BlueprintOffset { get => GetValue(BlueprintOffsetProperty); set => SetValue(BlueprintOffsetProperty, value); }
    public double BlueprintScale { get => GetValue(BlueprintScaleProperty); set => SetValue(BlueprintScaleProperty, value); }

    static Simulation3DControl()
    {
        PointsProperty.Changed.AddClassHandler<Simulation3DControl>((c, _) => c.RebuildGeometry());
        SettingsProperty.Changed.AddClassHandler<Simulation3DControl>((c, _) => c.RebuildGeometry());
        ShowGridProperty.Changed.AddClassHandler<Simulation3DControl>((c, _) => c.RequestNextFrameRendering());
        ShowPairLinksProperty.Changed.AddClassHandler<Simulation3DControl>((c, _) => c.RequestNextFrameRendering());
        ToolXRawProperty.Changed.AddClassHandler<Simulation3DControl>((c, _) => c.RequestNextFrameRendering());
        ToolYRawProperty.Changed.AddClassHandler<Simulation3DControl>((c, _) => c.RequestNextFrameRendering());
        ToolZRawProperty.Changed.AddClassHandler<Simulation3DControl>((c, _) => c.RequestNextFrameRendering());
        CurrentAlfaProperty.Changed.AddClassHandler<Simulation3DControl>((c, _) => c.RequestNextFrameRendering());
        CurrentBettaProperty.Changed.AddClassHandler<Simulation3DControl>((c, _) => c.RequestNextFrameRendering());
        ShowBlueprintsProperty.Changed.AddClassHandler<Simulation3DControl>((c, _) => c.RequestNextFrameRendering());
        BlueprintOpacityProperty.Changed.AddClassHandler<Simulation3DControl>((c, _) => c.RequestNextFrameRendering());
        BlueprintOffsetProperty.Changed.AddClassHandler<Simulation3DControl>((c, _) => c.RequestNextFrameRendering());
        BlueprintScaleProperty.Changed.AddClassHandler<Simulation3DControl>((c, _) => c.RequestNextFrameRendering());
    }

    protected override void OnOpenGlInit(GlInterface gl)
    {
        try
        {
            _failed = false;
            _loggedRenderSuccess = false;
            _failureDetails = null;
            _gl = GL.GetApi(gl.GetProcAddress);
            ConfigureLoggerFromSettings();
            LogInfo("OpenGL init started");
            CreateShadersWithFallback();

            _nozzleMesh = NozzleMeshBuilder.Build();
            _nozzleMesh.Upload(_gl);
            _jetConeMesh = ConeMeshBuilder.Build();
            _jetConeMesh.Upload(_gl);
            _quadMesh = BuildQuadMesh();
            _quadMesh.Upload(_gl);

            _blueprintPartTex = TextureLoader.LoadFromResource(_gl, "avares://RecipeStudio.Desktop/Assets/Images/H340_KAMA_1.fw.png");
            _blueprintManipulatorTex = TextureLoader.LoadFromResource(_gl, "avares://RecipeStudio.Desktop/Assets/Images/manipulator.fw.png");
            _blueprintNozzleTex = TextureLoader.LoadFromResource(_gl, "avares://RecipeStudio.Desktop/Assets/Images/soplo.fw.png");

            _dynamicLineVao = _gl.GenVertexArray();
            _dynamicLineVbo = _gl.GenBuffer();
            _gl.BindVertexArray(_dynamicLineVao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _dynamicLineVbo);
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(6 * sizeof(float)), null, BufferUsageARB.DynamicDraw);
            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), (void*)0);
            _gl.BindVertexArray(0);

            _glInitialized = true;
            LogInfo("OpenGL init success.");
            LogGlErrors("OnOpenGlInit");
            RebuildGeometry();
        }
        catch (Exception ex)
        {
            _failed = true;
            _failureDetails = $"{ex.GetType().Name}: {ex.Message}";
            LogError("OpenGL init failed", ex);
            Debug.WriteLine($"[Simulation3DControl] OpenGL init failed: {ex}");
        }
    }


    private void CreateShadersWithFallback()
    {
        var errors = new List<string>();

        bool TryCreate(string label, string meshVs, string meshFs, string lineVs, string lineFs, string texVs, string texFs, params (string Name, uint Location)[] bindings)
        {
            _meshShader?.Dispose(_gl!);
            _lineShader?.Dispose(_gl!);
            _texShader?.Dispose(_gl!);

            _meshShader = new ShaderProgram();
            _lineShader = new ShaderProgram();
            _texShader = new ShaderProgram();

            try
            {
                _meshShader.Create(_gl!, meshVs, meshFs, bindings);
                _lineShader.Create(_gl!, lineVs, lineFs, ("aPos", 0));
                _texShader.Create(_gl!, texVs, texFs, ("aPos", 0));
                _failureDetails = null;
                return true;
            }
            catch (Exception ex)
            {
                var msg = $"{label}: {ex.Message}";
                errors.Add(msg);
                LogWarn($"shader fallback failed: {msg}");
                return false;
            }
        }

        if (TryCreate("GLSL330", MeshVertexShader330, MeshFragmentShader330, LineVertexShader330, LineFragmentShader330, TexVertexShader330, TexFragmentShader330, ("aPos", 0), ("aNormal", 1)))
            return;

        if (TryCreate("GLSL300ES", MeshVertexShader300Es, MeshFragmentShader300Es, LineVertexShader300Es, LineFragmentShader300Es, TexVertexShader300Es, TexFragmentShader300Es, ("aPos", 0), ("aNormal", 1)))
            return;

        if (TryCreate("GLSL120", MeshVertexShader120, MeshFragmentShader120, LineVertexShader120, LineFragmentShader120, TexVertexShader120, TexFragmentShader120, ("aPos", 0), ("aNormal", 1)))
            return;

        if (TryCreate("GLSL100ES", MeshVertexShader100Es, MeshFragmentShader100Es, LineVertexShader100Es, LineFragmentShader100Es, TexVertexShader100Es, TexFragmentShader100Es, ("aPos", 0), ("aNormal", 1)))
            return;

        if (TryCreate("LEGACY", MeshVertexShaderLegacy, MeshFragmentShaderLegacy, LineVertexShaderLegacy, LineFragmentShaderLegacy, TexVertexShaderLegacy, TexFragmentShaderLegacy, ("aPos", 0), ("aNormal", 1)))
            return;

        var shaderError = "Shader fallback failed: " + string.Join(" | ", errors);
        LogError(shaderError, null);
        throw new InvalidOperationException(shaderError);
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        _glInitialized = false;
        _loggedRenderSuccess = false;
        _hasRenderedFrame = false;
        _failureDetails = null;
        _geometryDirty = true;
        if (_gl is null)
            return;

        _partMesh?.Dispose(_gl);
        _nozzleMesh?.Dispose(_gl);
        _jetConeMesh?.Dispose(_gl);
        _quadMesh?.Dispose(_gl);
        _meshShader?.Dispose(_gl);
        _lineShader?.Dispose(_gl);
        _texShader?.Dispose(_gl);

        if (_trajectoryVbo != 0) _gl.DeleteBuffer(_trajectoryVbo);
        if (_trajectoryVao != 0) _gl.DeleteVertexArray(_trajectoryVao);
        if (_targetTrajectoryVbo != 0) _gl.DeleteBuffer(_targetTrajectoryVbo);
        if (_targetTrajectoryVao != 0) _gl.DeleteVertexArray(_targetTrajectoryVao);
        if (_gridVbo != 0) _gl.DeleteBuffer(_gridVbo);
        if (_gridVao != 0) _gl.DeleteVertexArray(_gridVao);
        if (_blueprintPartTex != 0) _gl.DeleteTexture(_blueprintPartTex);
        if (_dynamicLineVbo != 0) _gl.DeleteBuffer(_dynamicLineVbo);
        if (_dynamicLineVao != 0) _gl.DeleteVertexArray(_dynamicLineVao);
        if (_blueprintManipulatorTex != 0) _gl.DeleteTexture(_blueprintManipulatorTex);
        if (_blueprintNozzleTex != 0) _gl.DeleteTexture(_blueprintNozzleTex);
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (_failed || _gl is null || _meshShader is null || _lineShader is null || _texShader is null)
            return;

        try
        {
            EnsureGeometryBuilt();
            _hasRenderedFrame = true;

            _gl.Enable(EnableCap.DepthTest);
            _gl.Enable(EnableCap.Blend);
            _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            _gl.ClearColor(0.05f, 0.09f, 0.15f, 1f);
            _gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));

            var view = _camera.GetView();
            var proj = _camera.GetProjection((float)Math.Max(1, Bounds.Width), (float)Math.Max(1, Bounds.Height));

            if (ShowGrid)
                DrawGrid(view, proj);

            DrawTrajectory(view, proj);

            if (ShowBlueprints)
                DrawBlueprints(view, proj);

            if (_partMesh is not null)
                DrawMesh(_partMesh, Matrix4x4.Identity, new Vector3(0.75f, 0.78f, 0.83f), 0.5f, view, proj);

            var nozzleBase = new Vector3((float)ToolXRaw, (float)ToolYRaw, (float)ToolZRaw);
            var currentTarget = GetCurrentTargetPoint(nozzleBase, out _, out _);
            var nozzleDir = SafeNormalize(currentTarget - nozzleBase, AnglesToDirection(CurrentAlfa, CurrentBetta));

            DrawNozzle(nozzleBase, nozzleDir, currentTarget, view, proj);
            LogGlErrors("OnOpenGlRender");

            if (!_loggedRenderSuccess)
            {
                _loggedRenderSuccess = true;
                LogInfo("first successful OpenGL frame rendered.");
            }

            RequestNextFrameRendering();
        }
        catch (Exception ex)
        {
            _failureDetails = $"{ex.GetType().Name}: {ex.Message}";
            LogError("OpenGL render failed", ex);
            _geometryDirty = true;
            Debug.WriteLine($"[Simulation3DControl] OpenGL render failed: {ex}");
            _hasRenderedFrame = false;
        }
    }


    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (_hasRenderedFrame)
            return;

        DrawSoftwareFallback(context);

        var message = _failed || !_glInitialized
            ? "OpenGL 3D сейчас недоступен — показан fallback"
            : "Инициализация 3D...";

        var ft = new FormattedText(
            message,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            13,
            Brushes.Orange);
        context.DrawText(ft, new Point(12, 12));

        if (!string.IsNullOrWhiteSpace(_failureDetails))
        {
            var details = new FormattedText(
                _failureDetails,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Consolas"),
                11,
                Brushes.OrangeRed);
            context.DrawText(details, new Point(12, 30));
        }
    }

    private void DrawSoftwareFallback(DrawingContext context)
    {
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(6, 20, 40)), Bounds);

        var points = Points?.Where(p => p.Act).ToList() ?? new List<RecipePoint>();
        if (points.Count < 2)
            return;

        var hZone = Settings?.HZone ?? 0;
        var tool = new List<Vector3>(points.Count);
        var target = new List<Vector3>(points.Count);
        foreach (var p in points)
        {
            var toolPos = new Vector3((float)(p.Xr0 + p.DX), (float)(p.Yx0 + p.DY), (float)(p.Zr0 + p.DZ));
            var t = p.GetTargetPoint(hZone);
            var radial = SafeNormalize(new Vector3(toolPos.X, toolPos.Y, 0f), Vector3.UnitX);
            var targetPos = radial * MathF.Abs((float)t.Xp) + new Vector3(0, 0, (float)t.Zp);
            if (!IsFinite(toolPos) || !IsFinite(targetPos))
                continue;
            tool.Add(toolPos);
            target.Add(targetPos);
        }

        if (tool.Count < 2 || target.Count < 2)
            return;

        static Point Project(Vector3 p)
        {
            var x = p.X - p.Y * 0.45f;
            var y = -p.Z + (p.X + p.Y) * 0.18f;
            return new Point(x, y);
        }

        var projectedAll = tool.Select(Project).Concat(target.Select(Project)).ToList();
        var minX = projectedAll.Min(p => p.X);
        var maxX = projectedAll.Max(p => p.X);
        var minY = projectedAll.Min(p => p.Y);
        var maxY = projectedAll.Max(p => p.Y);
        var w = Math.Max(1, maxX - minX);
        var h = Math.Max(1, maxY - minY);
        var scale = Math.Min((Bounds.Width - 24) / w, (Bounds.Height - 24) / h);

        Point ToScreen(Point p) => new(12 + (p.X - minX) * scale, 12 + (p.Y - minY) * scale);

        void DrawPath(IReadOnlyList<Vector3> path, Color color, double thickness)
        {
            if (path.Count < 2)
                return;

            var geometry = new StreamGeometry();
            using var gc = geometry.Open();
            gc.BeginFigure(ToScreen(Project(path[0])), false);
            for (var i = 1; i < path.Count; i++)
                gc.LineTo(ToScreen(Project(path[i])));
            gc.EndFigure(false);
            context.DrawGeometry(null, new Pen(new SolidColorBrush(color), thickness), geometry);
        }

        DrawPath(target, Color.FromRgb(255, 186, 64), 1.8);
        DrawPath(tool, Color.FromRgb(80, 190, 240), 2.2);

        var nozzleBase = new Vector3((float)ToolXRaw, (float)ToolYRaw, (float)ToolZRaw);
        var currentTarget = GetCurrentTargetPoint(nozzleBase, out _, out _);

        var nozzle = ToScreen(Project(nozzleBase));
        var impact = ToScreen(Project(currentTarget));

        context.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(255, 120, 80)), 1.8), nozzle, impact);
        context.DrawEllipse(new SolidColorBrush(Color.FromRgb(240, 80, 80)), new Pen(Brushes.White, 1), nozzle, 4, 4);
        context.DrawEllipse(new SolidColorBrush(Color.FromRgb(100, 240, 255)), new Pen(new SolidColorBrush(Color.FromRgb(210, 255, 255)), 1), impact, 3.5, 3.5);
    }

    private void DrawMesh(Mesh mesh, Matrix4x4 model, Vector3 color, float alpha, Matrix4x4 view, Matrix4x4 proj)
    {
        _meshShader!.Use(_gl!);
        _meshShader.SetMatrix(_gl!, "uModel", model);
        _meshShader.SetMatrix(_gl!, "uView", view);
        _meshShader.SetMatrix(_gl!, "uProjection", proj);
        _meshShader.SetVec3(_gl!, "uLightDir", Vector3.Normalize(new Vector3(-0.2f, -0.4f, -1f)));
        _meshShader.SetVec3(_gl!, "uColor", color);
        _meshShader.SetFloat(_gl!, "uAlpha", alpha);
        mesh.Draw(_gl!);
    }

    private void DrawTrajectory(Matrix4x4 view, Matrix4x4 proj)
    {
        if (_trajectoryVao == 0 || _trajectoryCount < 2)
            return;

        _lineShader!.Use(_gl!);
        _lineShader.SetMatrix(_gl!, "uModel", Matrix4x4.Identity);
        _lineShader.SetMatrix(_gl!, "uView", view);
        _lineShader.SetMatrix(_gl!, "uProjection", proj);

        _gl!.BindVertexArray(_trajectoryVao);
        _gl.LineWidth(2f);
        _lineShader.SetVec3(_gl, "uColor", new Vector3(0.35f, 0.72f, 0.95f));
        _lineShader.SetFloat(_gl, "uAlpha", 0.5f);
        _gl.DrawArrays(PrimitiveType.LineStrip, 0, (uint)_trajectoryCount);

        if (_targetTrajectoryVao != 0 && _targetTrajectoryCount > 1)
        {
            _gl.BindVertexArray(_targetTrajectoryVao);
            _gl.LineWidth(2f);
            _lineShader.SetVec3(_gl, "uColor", new Vector3(1f, 0.7f, 0.25f));
            _lineShader.SetFloat(_gl, "uAlpha", 0.55f);
            _gl.DrawArrays(PrimitiveType.LineStrip, 0, (uint)_targetTrajectoryCount);
        }

        var (passedCount, segmentIndex, segmentT) = CalculatePassedTrajectoryProgress();
        if (passedCount > 1)
        {
            _gl.BindVertexArray(_trajectoryVao);
            _gl.LineWidth(3.5f);
            _lineShader.SetVec3(_gl, "uColor", new Vector3(0.25f, 0.95f, 0.45f));
            _lineShader.SetFloat(_gl, "uAlpha", 1f);
            _gl.DrawArrays(PrimitiveType.LineStrip, 0, (uint)passedCount);

            if (_targetTrajectoryVao != 0 && _targetTrajectoryCount > 1)
            {
                _gl.BindVertexArray(_targetTrajectoryVao);
                _gl.LineWidth(3.5f);
                _lineShader.SetVec3(_gl, "uColor", new Vector3(1f, 0.85f, 0.35f));
                _lineShader.SetFloat(_gl, "uAlpha", 0.95f);
                _gl.DrawArrays(PrimitiveType.LineStrip, 0, (uint)Math.Min(passedCount, _targetTrajectoryCount));
            }
        }

        if (ShowPairLinks && _toolPath.Count > 1 && _targetPath.Count > 1)
        {
            var maxLines = 800;
            var step = Math.Max(1, _toolPath.Count / maxLines);
            for (var i = 0; i < _toolPath.Count && i < _targetPath.Count; i += step)
                DrawLine(view, proj, _toolPath[i], _targetPath[i], new Vector4(0.78f, 0.56f, 0.18f, 0.28f), 1f);

            if (segmentIndex >= 0)
            {
                var currentTarget = Vector3.Lerp(_targetPath[segmentIndex], _targetPath[Math.Min(segmentIndex + 1, _targetPath.Count - 1)], segmentT);
                DrawLine(view, proj, new Vector3((float)ToolXRaw, (float)ToolYRaw, (float)ToolZRaw), currentTarget, new Vector4(0.95f, 0.55f, 0.2f, 0.9f), 2f);
            }
        }

        _gl.BindVertexArray(0);
    }

    private void DrawGrid(Matrix4x4 view, Matrix4x4 proj)
    {
        if (_gridVao == 0 || _gridCount < 2)
            return;

        _lineShader!.Use(_gl!);
        _lineShader.SetMatrix(_gl!, "uModel", Matrix4x4.Identity);
        _lineShader.SetMatrix(_gl!, "uView", view);
        _lineShader.SetMatrix(_gl!, "uProjection", proj);
        _lineShader.SetVec3(_gl!, "uColor", new Vector3(0.35f, 0.35f, 0.45f));
        _lineShader.SetFloat(_gl!, "uAlpha", 0.35f);

        _gl!.BindVertexArray(_gridVao);
        _gl.LineWidth(1f);
        _gl.DrawArrays(PrimitiveType.Lines, 0, (uint)_gridCount);
        _gl.BindVertexArray(0);
    }

    private void DrawNozzle(Vector3 basePoint, Vector3 direction, Vector3 currentTarget, Matrix4x4 view, Matrix4x4 proj)
    {
        if (_nozzleMesh is null)
            return;

        var rot = CreateRotationFromForward(direction, Vector3.UnitZ);
        var model = rot * Matrix4x4.CreateTranslation(basePoint);
        DrawMesh(_nozzleMesh, model, new Vector3(0.95f, 0.32f, 0.2f), 1f, view, proj);

        var nozzleLength = 110f;
        var toTargetFromBase = currentTarget - basePoint;
        var distFromBase = toTargetFromBase.Length();
        var tipDistance = MathF.Min(nozzleLength, distFromBase);
        var tip = basePoint + direction * tipDistance;
        DrawLine(view, proj, basePoint, tip, new Vector4(1f, 0.35f, 0.2f, 1f), 2f);

        var toTarget = currentTarget - tip;
        var jetLen = toTarget.Length();
        if (_jetConeMesh is not null && jetLen > 1e-3f)
        {
            var jetDir = SafeNormalize(toTarget, direction);
            var jetRadius = Math.Clamp(jetLen * 0.12f, 8f, 60f);
            var jetModel = Matrix4x4.CreateScale(jetRadius, jetRadius, jetLen)
                * CreateRotationFromForward(jetDir, Vector3.UnitZ)
                * Matrix4x4.CreateTranslation(tip);
            DrawMesh(_jetConeMesh, jetModel, new Vector3(0.25f, 0.75f, 1f), 0.18f, view, proj);
        }

        DrawLine(view, proj, tip, currentTarget, new Vector4(0.35f, 0.8f, 1f, 0.95f), 2.25f);
        DrawImpactMarker(view, proj, currentTarget, 6f, new Vector4(0.4f, 0.95f, 1f, 0.95f));
    }

    private void DrawImpactMarker(Matrix4x4 view, Matrix4x4 proj, Vector3 center, float size, Vector4 color)
    {
        DrawLine(view, proj, center + new Vector3(-size, 0, 0), center + new Vector3(size, 0, 0), color, 2f);
        DrawLine(view, proj, center + new Vector3(0, -size, 0), center + new Vector3(0, size, 0), color, 2f);
        DrawLine(view, proj, center + new Vector3(0, 0, -size), center + new Vector3(0, 0, size), color, 2f);
    }

    private void DrawBlueprints(Matrix4x4 view, Matrix4x4 proj)
    {
        if (_quadMesh is null)
            return;

        DrawBlueprintPlane(_blueprintPartTex, Matrix4x4.CreateScale((float)(420 * BlueprintScale), 1, (float)(420 * BlueprintScale)) * Matrix4x4.CreateTranslation(BlueprintOffset), view, proj);
        DrawBlueprintPlane(_blueprintManipulatorTex, Matrix4x4.CreateScale((float)(280 * BlueprintScale), 1, (float)(240 * BlueprintScale)) * Matrix4x4.CreateRotationY(MathF.PI * 0.5f) * Matrix4x4.CreateTranslation(BlueprintOffset + new Vector3(200, 0, 120)), view, proj);
        DrawBlueprintPlane(_blueprintNozzleTex, Matrix4x4.CreateScale((float)(150 * BlueprintScale), 1, (float)(150 * BlueprintScale)) * Matrix4x4.CreateTranslation(new Vector3((float)ToolXRaw, (float)ToolYRaw + 60, (float)ToolZRaw + 50)), view, proj);
    }

    private void DrawBlueprintPlane(uint texture, Matrix4x4 model, Matrix4x4 view, Matrix4x4 proj)
    {
        if (texture == 0)
            return;

        _texShader!.Use(_gl!);
        _texShader.SetMatrix(_gl!, "uModel", model);
        _texShader.SetMatrix(_gl!, "uView", view);
        _texShader.SetMatrix(_gl!, "uProjection", proj);
        _texShader.SetFloat(_gl!, "uAlpha", (float)Math.Clamp(BlueprintOpacity, 0, 1));
        _texShader.SetInt(_gl!, "uTex0", 0);

        _gl!.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, texture);
        _gl.BindVertexArray(_quadMesh!.Vao);
        _gl.DrawElements(PrimitiveType.Triangles, (uint)_quadMesh.Indices.Length, DrawElementsType.UnsignedInt, null);
        _gl.BindVertexArray(0);
    }

    private void DrawLine(Matrix4x4 view, Matrix4x4 proj, Vector3 a, Vector3 b, Vector4 color, float width)
    {
        if (_dynamicLineVao == 0 || _dynamicLineVbo == 0)
            return;

        var vertices = stackalloc float[6] { a.X, a.Y, a.Z, b.X, b.Y, b.Z };

        _gl!.BindBuffer(BufferTargetARB.ArrayBuffer, _dynamicLineVbo);
        _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(6 * sizeof(float)), vertices);

        _lineShader!.Use(_gl);
        _lineShader.SetMatrix(_gl, "uModel", Matrix4x4.Identity);
        _lineShader.SetMatrix(_gl, "uView", view);
        _lineShader.SetMatrix(_gl, "uProjection", proj);
        _lineShader.SetVec3(_gl, "uColor", new Vector3(color.X, color.Y, color.Z));
        _lineShader.SetFloat(_gl, "uAlpha", color.W);

        _gl.LineWidth(width);
        _gl.BindVertexArray(_dynamicLineVao);
        _gl.DrawArrays(PrimitiveType.Lines, 0, 2);
        _gl.BindVertexArray(0);
    }


    private void ConfigureLoggerFromSettings()
    {
        if (_loggerConfigured)
            return;

        var s = Settings;
        _logger.Configure(s?.LoggingEnabled ?? true, s?.LogRetentionDays ?? 14, s?.LogMode ?? LogSeverity.Info, s?.LogsFolder);
        _loggerConfigured = true;
        LogInfo($"logger configured. folder={(s?.LogsFolder ?? "<default>")}");
    }

    private static string LocalDiagnosticsPath
        => Path.Combine(AppContext.BaseDirectory, "logs", "simulation3d.log");

    private void LogInfo(string message)
    {
        _logger.Info($"Simulation3DControl: {message}");
        WriteLocalDiag("INFO", message, null);
    }

    private void LogWarn(string message)
    {
        _logger.Warn($"Simulation3DControl: {message}");
        WriteLocalDiag("WARN", message, null);
    }

    private void LogError(string message, Exception? ex)
    {
        _logger.Error($"Simulation3DControl: {message}", ex);
        WriteLocalDiag("ERROR", message, ex);
    }

    private static void WriteLocalDiag(string level, string message, Exception? ex)
    {
        try
        {
            var dir = Path.GetDirectoryName(LocalDiagnosticsPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            using var sw = File.AppendText(LocalDiagnosticsPath);
            sw.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {level} {message}");
            if (ex is not null)
                sw.WriteLine(ex.ToString());
        }
        catch
        {
            // never throw from diagnostics
        }
    }


    private void LogGlErrors(string stage)
    {
        if (_gl is null)
            return;

        var hadErrors = false;
        for (var i = 0; i < 16; i++)
        {
            var err = _gl.GetError();
            if (err == GLEnum.NoError)
                break;

            hadErrors = true;
            LogWarn($"GL error at {stage}: {err}");
        }

        if (hadErrors)
            Debug.WriteLine($"[Simulation3DControl] GL errors detected at {stage}");
    }

    private void RebuildGeometry()
    {
        _geometryDirty = true;
        RequestNextFrameRendering();
    }

    private void EnsureGeometryBuilt()
    {
        if (!_geometryDirty || _gl is null)
            return;

        ConfigureLoggerFromSettings();

        try
        {
            RebuildPartMesh();
            RebuildTrajectory();
            RebuildGrid();
            _geometryDirty = false;
            LogInfo($"geometry rebuilt. partMesh={(_partMesh is null ? 0 : _partMesh.Vertices.Length / 6)} verts, toolPath={_toolPath.Count}, targetPath={_targetPath.Count}, gridVertices={_gridCount}");
            LogGlErrors("EnsureGeometryBuilt");
        }
        catch (Exception ex)
        {
            LogError("geometry rebuild failed", ex);
            throw;
        }
    }

    private void RebuildPartMesh()
    {
        _partMesh?.Dispose(_gl!);
        var src = Points?.Where(p => p.Act).ToList() ?? new List<RecipePoint>();
        var settings = Settings;

        var profile = src.Select(p => p.GetTargetPoint(settings?.HZone ?? 0))
            .Select(t => new Vector2((float)Math.Abs(t.Xp), (float)t.Zp))
            .GroupBy(v => MathF.Round(v.Y, 3))
            .Select(g => g.OrderByDescending(v => v.X).First())
            .OrderBy(v => v.Y)
            .ToList();

        _partMesh = LatheMeshBuilder.Build(profile, 96);
        _partMesh.Upload(_gl!);

        if (profile.Count > 1)
        {
            var minZ = profile.Min(v => v.Y);
            var maxZ = profile.Max(v => v.Y);
            var r = Math.Max(20, profile.Max(v => v.X));
            _camera.Fit(new Vector3(0, 0, (minZ + maxZ) * 0.5f), Math.Max(r, (maxZ - minZ) * 0.6f));
        }
    }

    private void RebuildTrajectory()
    {
        if (_trajectoryVbo != 0) _gl!.DeleteBuffer(_trajectoryVbo);
        if (_trajectoryVao != 0) _gl!.DeleteVertexArray(_trajectoryVao);
        if (_targetTrajectoryVbo != 0) _gl!.DeleteBuffer(_targetTrajectoryVbo);
        if (_targetTrajectoryVao != 0) _gl!.DeleteVertexArray(_targetTrajectoryVao);

        _toolPath = new List<Vector3>();
        _targetPath = new List<Vector3>();

        var points = Points?.Where(p => p.Act).ToList();
        if (points is null || points.Count == 0)
        {
            _trajectoryCount = 0;
            _targetTrajectoryCount = 0;
            return;
        }

        var hZone = Settings?.HZone ?? 0;
        foreach (var p in points)
        {
            var toolPos = new Vector3((float)(p.Xr0 + p.DX), (float)(p.Yx0 + p.DY), (float)(p.Zr0 + p.DZ));
            var target = p.GetTargetPoint(hZone);
            var radial = SafeNormalize(new Vector3(toolPos.X, toolPos.Y, 0f), Vector3.UnitX);
            var targetPos = radial * MathF.Abs((float)target.Xp) + new Vector3(0, 0, (float)target.Zp);

            if (!IsFinite(toolPos) || !IsFinite(targetPos))
                continue;

            _toolPath.Add(toolPos);
            _targetPath.Add(targetPos);
        }

        _trajectoryCount = _toolPath.Count;
        _targetTrajectoryCount = _targetPath.Count;

        if (_trajectoryCount > 0)
        {
            var data = LineStripBuilder.Build(_toolPath);
            _trajectoryVao = _gl!.GenVertexArray();
            _trajectoryVbo = _gl.GenBuffer();
            _gl.BindVertexArray(_trajectoryVao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _trajectoryVbo);
            unsafe
            {
                fixed (float* ptr = data)
                    _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * sizeof(float)), ptr, BufferUsageARB.StaticDraw);
            }

            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), (void*)0);
        }

        if (_targetTrajectoryCount > 0)
        {
            var targetData = LineStripBuilder.Build(_targetPath);
            _targetTrajectoryVao = _gl!.GenVertexArray();
            _targetTrajectoryVbo = _gl.GenBuffer();
            _gl.BindVertexArray(_targetTrajectoryVao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _targetTrajectoryVbo);
            unsafe
            {
                fixed (float* ptr = targetData)
                    _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(targetData.Length * sizeof(float)), ptr, BufferUsageARB.StaticDraw);
            }

            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), (void*)0);
        }

        _gl.BindVertexArray(0);
    }

    private void RebuildGrid()
    {
        if (_gridVbo != 0) _gl!.DeleteBuffer(_gridVbo);
        if (_gridVao != 0) _gl!.DeleteVertexArray(_gridVao);

        const int size = 1000;
        const int step = 50;
        var lines = new List<float>();
        for (var i = -size; i <= size; i += step)
        {
            lines.AddRange([i, -size, 0, i, size, 0]);
            lines.AddRange([-size, i, 0, size, i, 0]);
        }

        lines.AddRange([0, 0, 0, 180, 0, 0]);
        lines.AddRange([0, 0, 0, 0, 180, 0]);
        lines.AddRange([0, 0, 0, 0, 0, 180]);

        _gridCount = lines.Count / 3;
        _gridVao = _gl!.GenVertexArray();
        _gridVbo = _gl.GenBuffer();
        _gl.BindVertexArray(_gridVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _gridVbo);
        unsafe
        {
            fixed (float* ptr = lines.ToArray())
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(lines.Count * sizeof(float)), ptr, BufferUsageARB.StaticDraw);
        }

        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), (void*)0);
        _gl.BindVertexArray(0);
    }

    private (int PassedCount, int SegmentIndex, float SegmentT) CalculatePassedTrajectoryProgress()
    {
        if (_toolPath.Count == 0)
            return (0, -1, 0f);

        if (_toolPath.Count == 1)
            return (1, 0, 0f);

        var position = new Vector3((float)ToolXRaw, (float)ToolYRaw, (float)ToolZRaw);
        var bestDistance = float.MaxValue;
        var bestSegment = 0;
        var bestT = 0f;

        for (var i = 0; i < _toolPath.Count - 1; i++)
        {
            var a = _toolPath[i];
            var b = _toolPath[i + 1];
            var ab = b - a;
            var lenSq = ab.LengthSquared();
            var t = lenSq < 1e-8f ? 0f : Clamp01(Vector3.Dot(position - a, ab) / lenSq);
            var projected = a + ab * t;
            var d = Vector3.DistanceSquared(position, projected);
            if (d < bestDistance)
            {
                bestDistance = d;
                bestSegment = i;
                bestT = t;
            }
        }

        return (Math.Clamp(bestSegment + 1, 0, _trajectoryCount), bestSegment, bestT);
    }

    private Vector3 GetCurrentTargetPoint(Vector3 nozzleBase, out int segmentIndex, out float segmentT)
    {
        if (_targetPath.Count == 0)
        {
            segmentIndex = -1;
            segmentT = 0f;
            return nozzleBase + AnglesToDirection(CurrentAlfa, CurrentBetta) * 80f;
        }

        if (_targetPath.Count == 1 || _toolPath.Count < 2)
        {
            segmentIndex = 0;
            segmentT = 0f;
            return _targetPath[0];
        }

        var bestDistance = float.MaxValue;
        segmentIndex = 0;
        segmentT = 0f;

        for (var i = 0; i < _toolPath.Count - 1; i++)
        {
            var a = _toolPath[i];
            var b = _toolPath[i + 1];
            var ab = b - a;
            var lenSq = ab.LengthSquared();
            var t = lenSq < 1e-8f ? 0f : Clamp01(Vector3.Dot(nozzleBase - a, ab) / lenSq);
            var projected = a + ab * t;
            var d = Vector3.DistanceSquared(nozzleBase, projected);
            if (d < bestDistance)
            {
                bestDistance = d;
                segmentIndex = i;
                segmentT = t;
            }
        }

        var i1 = Math.Min(segmentIndex + 1, _targetPath.Count - 1);
        return Vector3.Lerp(_targetPath[segmentIndex], _targetPath[i1], segmentT);
    }


    private static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);

    private static Vector3 SafeNormalize(Vector3 value, Vector3 fallback)
    {
        var lenSq = value.LengthSquared();
        if (lenSq < 1e-8f || float.IsNaN(lenSq) || float.IsInfinity(lenSq))
            return fallback;

        var v = value / MathF.Sqrt(lenSq);
        return IsFinite(v) ? v : fallback;
    }

    private static bool IsFinite(Vector3 v)
        => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);

    private static Vector3 AnglesToDirection(double alfaDeg, double bettaDeg)
    {
        var a = (float)(alfaDeg * Math.PI / 180.0);
        var b = (float)(bettaDeg * Math.PI / 180.0);
        var dir = new Vector3(MathF.Cos(b) * MathF.Cos(a), MathF.Sin(b), -MathF.Cos(b) * MathF.Sin(a));
        return SafeNormalize(dir, Vector3.UnitX);
    }

    private static Matrix4x4 CreateRotationFromForward(Vector3 forward, Vector3 up)
    {
        var f = SafeNormalize(forward, Vector3.UnitZ);
        var r = SafeNormalize(Vector3.Cross(up, f), Vector3.UnitY);
        var u = SafeNormalize(Vector3.Cross(f, r), Vector3.UnitX);

        return new Matrix4x4(
            r.X, r.Y, r.Z, 0,
            u.X, u.Y, u.Z, 0,
            f.X, f.Y, f.Z, 0,
            0, 0, 0, 1);
    }

    private static Mesh BuildQuadMesh()
    {
        var vertices = new float[]
        {
            -0.5f, 0f, -0.5f, 0, 0, 1,
             0.5f, 0f, -0.5f, 0, 0, 1,
             0.5f, 0f,  0.5f, 0, 0, 1,
            -0.5f, 0f,  0.5f, 0, 0, 1
        };
        var indices = new uint[] { 0, 1, 2, 0, 2, 3 };
        return new Mesh(vertices, indices);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _last = e.GetPosition(this);
        var props = e.GetCurrentPoint(this).Properties;
        _isPanning = props.IsRightButtonPressed || e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (e.ClickCount == 2)
        {
            RebuildGeometry();
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var props = e.GetCurrentPoint(this).Properties;
        if (!props.IsLeftButtonPressed && !props.IsRightButtonPressed)
            return;

        var p = e.GetPosition(this);
        var dx = (float)(p.X - _last.X);
        var dy = (float)(p.Y - _last.Y);
        _last = p;

        if (_isPanning)
            _camera.Pan(dx, dy);
        else
            _camera.Rotate(dx, dy);

        RequestNextFrameRendering();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        _camera.Zoom((float)(e.Delta.Y * 60));
        RequestNextFrameRendering();
    }

    private const string MeshVertexShader330 = "#version 330 core\nlayout(location=0) in vec3 aPos;layout(location=1) in vec3 aNormal;uniform mat4 uModel;uniform mat4 uView;uniform mat4 uProjection;out vec3 vNormal;void main(){vNormal=mat3(uModel)*aNormal;gl_Position=uProjection*uView*uModel*vec4(aPos,1.0);}";
    private const string MeshFragmentShader330 = "#version 330 core\nin vec3 vNormal;uniform vec3 uLightDir;uniform vec3 uColor;uniform float uAlpha;out vec4 FragColor;void main(){vec3 n=normalize(vNormal);float diff=max(dot(n,normalize(-uLightDir)),0.2);vec3 col=uColor*(0.25+0.75*diff);FragColor=vec4(col,uAlpha);}";
    private const string LineVertexShader330 = "#version 330 core\nlayout(location=0) in vec3 aPos;uniform mat4 uModel;uniform mat4 uView;uniform mat4 uProjection;void main(){gl_Position=uProjection*uView*uModel*vec4(aPos,1.0);}";
    private const string LineFragmentShader330 = "#version 330 core\nuniform vec3 uColor;uniform float uAlpha;out vec4 FragColor;void main(){FragColor=vec4(uColor,uAlpha);}";
    private const string TexVertexShader330 = "#version 330 core\nlayout(location=0) in vec3 aPos;uniform mat4 uModel;uniform mat4 uView;uniform mat4 uProjection;out vec2 vUv;void main(){vUv=aPos.xz+vec2(0.5,0.5);gl_Position=uProjection*uView*uModel*vec4(aPos,1.0);}";
    private const string TexFragmentShader330 = "#version 330 core\nin vec2 vUv;uniform sampler2D uTex0;uniform float uAlpha;out vec4 FragColor;void main(){vec4 c=texture(uTex0,vUv);FragColor=vec4(c.rgb,c.a*uAlpha);}";


    private const string MeshVertexShader300Es = "#version 300 es\nprecision mediump float;layout(location=0) in vec3 aPos;layout(location=1) in vec3 aNormal;uniform mat4 uModel;uniform mat4 uView;uniform mat4 uProjection;out vec3 vNormal;void main(){vNormal=mat3(uModel)*aNormal;gl_Position=uProjection*uView*uModel*vec4(aPos,1.0);}";
    private const string MeshFragmentShader300Es = "#version 300 es\nprecision mediump float;in vec3 vNormal;uniform vec3 uLightDir;uniform vec3 uColor;uniform float uAlpha;out vec4 FragColor;void main(){vec3 n=normalize(vNormal);float diff=max(dot(n,normalize(-uLightDir)),0.2);vec3 col=uColor*(0.25+0.75*diff);FragColor=vec4(col,uAlpha);}";
    private const string LineVertexShader300Es = "#version 300 es\nprecision mediump float;layout(location=0) in vec3 aPos;uniform mat4 uModel;uniform mat4 uView;uniform mat4 uProjection;void main(){gl_Position=uProjection*uView*uModel*vec4(aPos,1.0);}";
    private const string LineFragmentShader300Es = "#version 300 es\nprecision mediump float;uniform vec3 uColor;uniform float uAlpha;out vec4 FragColor;void main(){FragColor=vec4(uColor,uAlpha);}";
    private const string TexVertexShader300Es = "#version 300 es\nprecision mediump float;layout(location=0) in vec3 aPos;uniform mat4 uModel;uniform mat4 uView;uniform mat4 uProjection;out vec2 vUv;void main(){vUv=aPos.xz+vec2(0.5,0.5);gl_Position=uProjection*uView*uModel*vec4(aPos,1.0);}";
    private const string TexFragmentShader300Es = "#version 300 es\nprecision mediump float;in vec2 vUv;uniform sampler2D uTex0;uniform float uAlpha;out vec4 FragColor;void main(){vec4 c=texture(uTex0,vUv);FragColor=vec4(c.rgb,c.a*uAlpha);}";
    private const string MeshVertexShader120 = "#version 120\nattribute vec3 aPos;attribute vec3 aNormal;uniform mat4 uModel;uniform mat4 uView;uniform mat4 uProjection;varying vec3 vNormal;void main(){vNormal=mat3(uModel)*aNormal;gl_Position=uProjection*uView*uModel*vec4(aPos,1.0);}";
    private const string MeshFragmentShader120 = "#version 120\nvarying vec3 vNormal;uniform vec3 uLightDir;uniform vec3 uColor;uniform float uAlpha;void main(){vec3 n=normalize(vNormal);float diff=max(dot(n,normalize(-uLightDir)),0.2);vec3 col=uColor*(0.25+0.75*diff);gl_FragColor=vec4(col,uAlpha);}";
    private const string LineVertexShader120 = "#version 120\nattribute vec3 aPos;uniform mat4 uModel;uniform mat4 uView;uniform mat4 uProjection;void main(){gl_Position=uProjection*uView*uModel*vec4(aPos,1.0);}";
    private const string LineFragmentShader120 = "#version 120\nuniform vec3 uColor;uniform float uAlpha;void main(){gl_FragColor=vec4(uColor,uAlpha);}";
    private const string TexVertexShader120 = "#version 120\nattribute vec3 aPos;uniform mat4 uModel;uniform mat4 uView;uniform mat4 uProjection;varying vec2 vUv;void main(){vUv=aPos.xz+vec2(0.5,0.5);gl_Position=uProjection*uView*uModel*vec4(aPos,1.0);}";
    private const string TexFragmentShader120 = "#version 120\nvarying vec2 vUv;uniform sampler2D uTex0;uniform float uAlpha;void main(){vec4 c=texture2D(uTex0,vUv);gl_FragColor=vec4(c.rgb,c.a*uAlpha);}";

    private const string MeshVertexShader100Es = "#version 100\nattribute vec3 aPos;attribute vec3 aNormal;uniform mat4 uModel;uniform mat4 uView;uniform mat4 uProjection;varying vec3 vNormal;void main(){vNormal=mat3(uModel)*aNormal;gl_Position=uProjection*uView*uModel*vec4(aPos,1.0);}";
    private const string MeshFragmentShader100Es = "#version 100\nprecision mediump float;varying vec3 vNormal;uniform vec3 uLightDir;uniform vec3 uColor;uniform float uAlpha;void main(){vec3 n=normalize(vNormal);float diff=max(dot(n,normalize(-uLightDir)),0.2);vec3 col=uColor*(0.25+0.75*diff);gl_FragColor=vec4(col,uAlpha);}";
    private const string LineVertexShader100Es = "#version 100\nattribute vec3 aPos;uniform mat4 uModel;uniform mat4 uView;uniform mat4 uProjection;void main(){gl_Position=uProjection*uView*uModel*vec4(aPos,1.0);}";
    private const string LineFragmentShader100Es = "#version 100\nprecision mediump float;uniform vec3 uColor;uniform float uAlpha;void main(){gl_FragColor=vec4(uColor,uAlpha);}";
    private const string TexVertexShader100Es = "#version 100\nattribute vec3 aPos;uniform mat4 uModel;uniform mat4 uView;uniform mat4 uProjection;varying vec2 vUv;void main(){vUv=aPos.xz+vec2(0.5,0.5);gl_Position=uProjection*uView*uModel*vec4(aPos,1.0);}";
    private const string TexFragmentShader100Es = "#version 100\nprecision mediump float;varying vec2 vUv;uniform sampler2D uTex0;uniform float uAlpha;void main(){vec4 c=texture2D(uTex0,vUv);gl_FragColor=vec4(c.rgb,c.a*uAlpha);}";

    private const string MeshVertexShaderLegacy = "attribute vec3 aPos;attribute vec3 aNormal;uniform mat4 uModel;uniform mat4 uView;uniform mat4 uProjection;varying vec3 vNormal;void main(){vNormal=mat3(uModel)*aNormal;gl_Position=uProjection*uView*uModel*vec4(aPos,1.0);}";
    private const string MeshFragmentShaderLegacy = "varying vec3 vNormal;uniform vec3 uLightDir;uniform vec3 uColor;uniform float uAlpha;void main(){vec3 n=normalize(vNormal);float diff=max(dot(n,normalize(-uLightDir)),0.2);vec3 col=uColor*(0.25+0.75*diff);gl_FragColor=vec4(col,uAlpha);}";
    private const string LineVertexShaderLegacy = "attribute vec3 aPos;uniform mat4 uModel;uniform mat4 uView;uniform mat4 uProjection;void main(){gl_Position=uProjection*uView*uModel*vec4(aPos,1.0);}";
    private const string LineFragmentShaderLegacy = "uniform vec3 uColor;uniform float uAlpha;void main(){gl_FragColor=vec4(uColor,uAlpha);}";
    private const string TexVertexShaderLegacy = "attribute vec3 aPos;uniform mat4 uModel;uniform mat4 uView;uniform mat4 uProjection;varying vec2 vUv;void main(){vUv=aPos.xz+vec2(0.5,0.5);gl_Position=uProjection*uView*uModel*vec4(aPos,1.0);}";
    private const string TexFragmentShaderLegacy = "varying vec2 vUv;uniform sampler2D uTex0;uniform float uAlpha;void main(){vec4 c=texture2D(uTex0,vUv);gl_FragColor=vec4(c.rgb,c.a*uAlpha);}";
}
