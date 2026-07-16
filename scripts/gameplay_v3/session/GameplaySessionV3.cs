using GameplayV3.Company;
using GameplayV3.Mercenary;
using GameplayV3.Control;
using GameplayV3.Resources;
using GameplayV3.Work;
using GameplayV3.Stockpile;
using GameplayV3.Construction;
using GameplayV3.Needs;
using GameplayV3.Farming;
using GameplayV3.Jobs;

namespace GameplayV3.Session;

public static class GameplaySessionV3
{
    private static CompanySessionV3? _currentCompanySession;
    private static MercenarySessionV3? _currentMercenarySession;
    private static MercenaryControlSessionV3? _currentControlSession;
    private static ResourceSessionV3? _currentResourceSession;
    private static MercenaryWorkSessionV3? _currentWorkSession;
    private static StockpileSessionV3? _currentStockpileSession;
    private static ConstructionSessionV3? _currentConstructionSession;
    private static MercenaryNeedsSessionV3? _currentNeedsSession;
    private static FarmSessionV3? _currentFarmSession;
    private static JobManagerV3? _currentJobManager;
    private static long _sessionRevision;

    public static long SessionRevision => _sessionRevision;

    public static void BeginNewSession()
    {
        _sessionRevision++;
        _currentCompanySession = new CompanySessionV3();
        _currentMercenarySession = new MercenarySessionV3(_currentCompanySession.CompanyRegistry);
        _currentControlSession = new MercenaryControlSessionV3(_sessionRevision, _currentCompanySession, _currentMercenarySession);
        _currentResourceSession = new ResourceSessionV3();
        _currentStockpileSession = new StockpileSessionV3();
        _currentConstructionSession = new ConstructionSessionV3();
        _currentNeedsSession = new MercenaryNeedsSessionV3(_sessionRevision);
        _currentFarmSession = new FarmSessionV3(_sessionRevision);
        _currentJobManager = new JobManagerV3(_sessionRevision);
        _currentWorkSession = new MercenaryWorkSessionV3(_sessionRevision,_currentCompanySession,_currentMercenarySession,_currentResourceSession,_currentStockpileSession,_currentControlSession);
        AttachNeedsPolicy();
        _currentControlSession.AttachWorkSession(_currentWorkSession);
    }

    public static bool EnsureCompanyCoreInitialized(
        out CompanySessionV3 companySession,
        out bool createdNow,
        out string reason)
    {
        EnsureSessionBundle();
        companySession = _currentCompanySession!;
        return companySession.TryInitializeLocalSinglePlayer(out createdNow, out reason);
    }

    public static bool TryGetCompanySession(out CompanySessionV3? companySession)
    {
        companySession = _currentCompanySession;
        return companySession?.IsInitialized == true;
    }

    public static bool EnsureMercenarySession(
        CompanySessionV3 companySession,
        out MercenarySessionV3 mercenarySession,
        out string reason)
    {
        if (!ReferenceEquals(companySession, _currentCompanySession))
        {
            mercenarySession = null!;
            reason = "MercenarySession must use the current GameplaySession CompanySession.";
            return false;
        }

        _currentMercenarySession ??= new MercenarySessionV3(companySession.CompanyRegistry);
        _currentControlSession ??= new MercenaryControlSessionV3(_sessionRevision, companySession, _currentMercenarySession);
        mercenarySession = _currentMercenarySession;
        reason = string.Empty;
        return true;
    }

    public static bool TryGetMercenarySession(out MercenarySessionV3? mercenarySession)
    {
        mercenarySession = _currentMercenarySession;
        return mercenarySession != null;
    }

    public static bool EnsureControlSession(CompanySessionV3 companySession, MercenarySessionV3 mercenarySession, out MercenaryControlSessionV3 controlSession, out string reason)
    {
        if (!ReferenceEquals(companySession, _currentCompanySession) || !ReferenceEquals(mercenarySession, _currentMercenarySession))
        { controlSession=null!;reason="ControlSession must use the current GameplaySession sessions.";return false; }
        _currentControlSession ??= new MercenaryControlSessionV3(_sessionRevision,companySession,mercenarySession);
        controlSession=_currentControlSession;reason=string.Empty;return true;
    }

    public static bool TryGetControlSession(out MercenaryControlSessionV3? controlSession){controlSession=_currentControlSession;return controlSession!=null;}
    public static bool EnsureResourceAndWorkSessions(CompanySessionV3 company,MercenarySessionV3 mercenary,MercenaryControlSessionV3 control,out ResourceSessionV3 resources,out MercenaryWorkSessionV3 work,out string reason)
    {if(!ReferenceEquals(company,_currentCompanySession)||!ReferenceEquals(mercenary,_currentMercenarySession)||!ReferenceEquals(control,_currentControlSession)){resources=null!;work=null!;reason="Resource/Work sessions must use the current GameplaySession bundle.";return false;}_currentResourceSession??=new();_currentStockpileSession??=new();_currentNeedsSession??=new(_sessionRevision);_currentWorkSession??=new(_sessionRevision,company,mercenary,_currentResourceSession,_currentStockpileSession,control);AttachNeedsPolicy();control.AttachWorkSession(_currentWorkSession);resources=_currentResourceSession;work=_currentWorkSession;reason=string.Empty;return true;}
    public static bool TryGetResourceSession(out ResourceSessionV3? resources){resources=_currentResourceSession;return resources!=null;}
    public static bool TryGetWorkSession(out MercenaryWorkSessionV3? work){work=_currentWorkSession;return work!=null;}
    public static bool TryGetStockpileSession(out StockpileSessionV3? stockpiles){stockpiles=_currentStockpileSession;return stockpiles!=null;}
    public static bool TryGetConstructionSession(out ConstructionSessionV3? construction){construction=_currentConstructionSession;return construction!=null;}
    public static bool TryGetNeedsSession(out MercenaryNeedsSessionV3? needs){needs=_currentNeedsSession;return needs!=null;}
    public static bool TryGetFarmSession(out FarmSessionV3? farm){farm=_currentFarmSession;return farm!=null;}
    public static bool TryGetJobManager(out JobManagerV3? jobs){jobs=_currentJobManager;return jobs!=null&&jobs.SessionRevision==_sessionRevision;}
    public static bool IsCurrentWorkSession(MercenaryWorkSessionV3 work)=>ReferenceEquals(work,_currentWorkSession)&&work.SessionRevision==_sessionRevision;
    public static bool IsCurrentControlSession(MercenaryControlSessionV3 session)=>ReferenceEquals(session,_currentControlSession)&&session.SessionRevision==_sessionRevision;

    private static void EnsureSessionBundle()
    {
        if(_currentCompanySession!=null)return;
        _sessionRevision++;
        _currentCompanySession=new CompanySessionV3();
        _currentMercenarySession=new MercenarySessionV3(_currentCompanySession.CompanyRegistry);
        _currentControlSession=new MercenaryControlSessionV3(_sessionRevision,_currentCompanySession,_currentMercenarySession);
        _currentResourceSession=new ResourceSessionV3();
        _currentStockpileSession=new StockpileSessionV3();
        _currentConstructionSession=new ConstructionSessionV3();
        _currentNeedsSession=new MercenaryNeedsSessionV3(_sessionRevision);
        _currentFarmSession=new FarmSessionV3(_sessionRevision);
        _currentJobManager=new JobManagerV3(_sessionRevision);
        _currentWorkSession=new MercenaryWorkSessionV3(_sessionRevision,_currentCompanySession,_currentMercenarySession,_currentResourceSession,_currentStockpileSession,_currentControlSession);
        AttachNeedsPolicy();
        _currentControlSession.AttachWorkSession(_currentWorkSession);
    }
    private static void AttachNeedsPolicy(){if(_currentWorkSession==null||_currentNeedsSession==null)return;_currentWorkSession.AttachStartPolicy((id,type)=>(_currentNeedsSession.CanStartWork(id,type,out string reason),reason));}
}
