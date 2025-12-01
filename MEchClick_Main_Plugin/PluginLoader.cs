using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using PartManager.UI;
using PartManager.Data;

[assembly: ExtensionApplication(typeof(PartManager.PluginLoader))]

namespace PartManager
{
    public class PluginLoader : IExtensionApplication
    {
        public void Initialize()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc != null)
            {
                var ed = doc.Editor;
                ed.WriteMessage("\n");
                ed.WriteMessage("\n╔════════════════════════════════════════════════╗");
                ed.WriteMessage("\n║     Part Manager Plugin 로드 완료              ║");
                ed.WriteMessage("\n║     Commands: SHOWUI, HIDEUI, TOGGLEUI, DOCKUI ║");
                ed.WriteMessage("\n╚════════════════════════════════════════════════╝");
                ed.WriteMessage("\n");
            }

            // 데이터베이스 초기화
            System.Diagnostics.Debug.WriteLine("[Loader] DatabaseManager 초기화 시작");
            try
            {
                DatabaseManager.Instance.Initialize();
                System.Diagnostics.Debug.WriteLine("[Loader] DatabaseManager 초기화 완료");
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Loader] DatabaseManager 초기화 실패: {ex.Message}");
            }

            // UI 초기화
            System.Diagnostics.Debug.WriteLine("[Loader] PaletteManager 초기화 시작");
            PaletteManager.Initialize();

            System.Diagnostics.Debug.WriteLine("[Loader] PaletteManager Show 호출");
            PaletteManager.Show();

            System.Diagnostics.Debug.WriteLine("[Loader] 초기화 완료");

            // 디버그 정보 출력
            if (doc != null)
            {
                var settings = PaletteSettings.Load();
                doc.Editor.WriteMessage($"\n도킹 위치: {settings.DockPosition}");
                doc.Editor.WriteMessage($"\n위치: {settings.Location}");
                doc.Editor.WriteMessage($"\n크기: {settings.FloatingSize}");
                doc.Editor.WriteMessage("\n");
            }
        }

        public void Terminate()
        {
            System.Diagnostics.Debug.WriteLine("[Loader] 종료 시작");
            PaletteManager.Cleanup();
            DatabaseManager.Instance.Dispose();

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc != null)
            {
                doc.Editor.WriteMessage("\n✅ Part Manager 설정이 저장되었습니다.");
            }
        }
    }
}
