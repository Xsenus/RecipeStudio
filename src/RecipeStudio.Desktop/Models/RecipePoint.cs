using System;
using RecipeStudio.Desktop.ViewModels;

namespace RecipeStudio.Desktop.Models;

public sealed class RecipePoint : ViewModelBase
{
    private string _recipeCode = "";
    private int _nPoint;
    private bool _act;
    private bool _safe;

    private double _rCrd;
    private double _zCrd;
    private int _place; // 0 - bottom (normal), 1 - top (mirrored) as in the provided excel
    private bool _hidden;

    private double _aNozzle;
    private double _recommendedAlfa;
    private double _alfa;
    private double _betta;

    private double _speedTable;
    private double _timeSec;
    private double _nozzleSpeed;

    private double _recommendedIceRate;
    private double _iceRate;
    private double _iceGrind;
    private double _airPressure;
    private double _airTemp;

    private bool _container;
    private double _dClampForm;
    private double _dClampCont;
    private string? _description;

    // SAVE / CALC outputs (the TSV contains these columns already)
    private double _xr0;
    private double _yx0;
    private double _zr0;
    private double _dX;
    private double _dY;
    private double _dZ;
    private double _dA;
    private double _aB;

    private double _xPuls;
    private double _yPuls;
    private double _zPuls;
    private double _aPuls;
    private double _bPuls;

    private double _topPuls;
    private double _topHz;
    private double _lowPuls;
    private double _lowHz;
    private double _clampPuls;

    public string RecipeCode { get => _recipeCode; set => SetProperty(ref _recipeCode, value); }
    public int NPoint { get => _nPoint; set => SetProperty(ref _nPoint, value); }
    public bool Act { get => _act; set => SetProperty(ref _act, value); }
    public bool Safe { get => _safe; set => SetProperty(ref _safe, value); }

    public double RCrd { get => _rCrd; set => SetProperty(ref _rCrd, value); }
    public double ZCrd { get => _zCrd; set => SetProperty(ref _zCrd, value); }

    /// <summary>
    /// 0 - normal (bottom), 1 - mirrored (top). Kept int to match file format.
    /// </summary>
    public int Place { get => _place; set => SetProperty(ref _place, value); }

    public bool Hidden { get => _hidden; set => SetProperty(ref _hidden, value); }

    public double ANozzle { get => _aNozzle; set => SetProperty(ref _aNozzle, value); }

    /// <summary>
    /// Recommended alpha computed from neighbour points.
    /// </summary>
    public double RecommendedAlfa { get => _recommendedAlfa; set => SetProperty(ref _recommendedAlfa, value); }

    public double Alfa { get => _alfa; set => SetProperty(ref _alfa, value); }
    public double Betta { get => _betta; set => SetProperty(ref _betta, value); }

    public double SpeedTable { get => _speedTable; set => SetProperty(ref _speedTable, value); }

    public double TimeSec { get => _timeSec; set => SetProperty(ref _timeSec, value); }

    public double NozzleSpeedMmMin { get => _nozzleSpeed; set => SetProperty(ref _nozzleSpeed, value); }

    public double RecommendedIceRate { get => _recommendedIceRate; set => SetProperty(ref _recommendedIceRate, value); }

    public double IceRate { get => _iceRate; set => SetProperty(ref _iceRate, value); }
    public double IceGrind { get => _iceGrind; set => SetProperty(ref _iceGrind, value); }
    public double AirPressure { get => _airPressure; set => SetProperty(ref _airPressure, value); }
    public double AirTemp { get => _airTemp; set => SetProperty(ref _airTemp, value); }

    public bool Container { get => _container; set => SetProperty(ref _container, value); }
    public double DClampForm { get => _dClampForm; set => SetProperty(ref _dClampForm, value); }
    public double DClampCont { get => _dClampCont; set => SetProperty(ref _dClampCont, value); }
    public string? Description { get => _description; set => SetProperty(ref _description, value); }

    public double Xr0 { get => _xr0; set => SetProperty(ref _xr0, value); }
    public double Yx0 { get => _yx0; set => SetProperty(ref _yx0, value); }
    public double Zr0 { get => _zr0; set => SetProperty(ref _zr0, value); }

    public double DX { get => _dX; set => SetProperty(ref _dX, value); }
    public double DY { get => _dY; set => SetProperty(ref _dY, value); }
    public double DZ { get => _dZ; set => SetProperty(ref _dZ, value); }

    public double DA { get => _dA; set => SetProperty(ref _dA, value); }
    public double AB { get => _aB; set => SetProperty(ref _aB, value); }

    public double XPuls { get => _xPuls; set => SetProperty(ref _xPuls, value); }
    public double YPuls { get => _yPuls; set => SetProperty(ref _yPuls, value); }
    public double ZPuls { get => _zPuls; set => SetProperty(ref _zPuls, value); }
    public double APuls { get => _aPuls; set => SetProperty(ref _aPuls, value); }
    public double BPuls { get => _bPuls; set => SetProperty(ref _bPuls, value); }

    public double TopPuls { get => _topPuls; set => SetProperty(ref _topPuls, value); }
    public double TopHz { get => _topHz; set => SetProperty(ref _topHz, value); }
    public double LowPuls { get => _lowPuls; set => SetProperty(ref _lowPuls, value); }
    public double LowHz { get => _lowHz; set => SetProperty(ref _lowHz, value); }
    public double ClampPuls { get => _clampPuls; set => SetProperty(ref _clampPuls, value); }

    // Convenience for plotting
    public (double Xp, double Zp) GetTargetPoint(double hZone)
    {
        var xp = Place == 0 ? RCrd : -RCrd;
        var zp = Place == 0 ? ZCrd : hZone - ZCrd;
        return (xp, zp);
    }

    public override string ToString()
        => $"#{NPoint} R={RCrd} Z={ZCrd} Place={Place} Safe={(Safe ? 1 : 0)}";

    public RecipePoint Clone()
        => (RecipePoint)MemberwiseClone();
}
