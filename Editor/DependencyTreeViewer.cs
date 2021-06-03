using System.Collections.Generic;
using System.Linq;

using UnityEditor;

using UnityEngine;


namespace FreakshowStudio.NugetForUnity.Editor
{
    /// <summary>
    /// A viewer for all of the packages and their dependencies currently installed in the project.
    /// </summary>
    public class DependencyTreeViewer : EditorWindow
    {
        /// <summary>
        /// Opens the NuGet Package Manager Window.
        /// </summary>
        [MenuItem("NuGet/Show Dependency Tree", false, 5)]
        protected static void DisplayDependencyTree()
        {
            GetWindow<DependencyTreeViewer>();
        }

        /// <summary>
        /// The titles of the tabs in the window.
        /// </summary>
        private readonly string[] _tabTitles = { "Dependency Tree", "Who Depends on Me?" };

        /// <summary>
        /// The currently selected tab in the window.
        /// </summary>
        private int _currentTab;

        private int _selectedPackageIndex = -1;

        /// <summary>
        /// The list of packages that depend on the specified package.
        /// </summary>
        private readonly List<NugetPackage> _parentPackages = new();

        /// <summary>
        /// The list of currently installed packages.
        /// </summary>
        private List<NugetPackage> _installedPackages;

        /// <summary>
        /// The array of currently installed package IDs.
        /// </summary>
        private string[] _installedPackageIds;

        private readonly Dictionary<NugetPackage, bool> _expanded = new();

        private List<NugetPackage> _roots;

        private Vector2 _scrollPosition;

        /// <summary>
        /// Called when enabling the window.
        /// </summary>
        private void OnEnable()
        {
            try
            {
                // reload the NuGet.config file, in case it was changed after Unity opened, but before the manager window opened (now)
                NugetHelper.LoadNugetConfigFile();

                // set the window title
                titleContent = new GUIContent("Dependencies");

                EditorUtility.DisplayProgressBar("Building Dependency Tree", "Reading installed packages...", 0.5f);

                NugetHelper.UpdateInstalledPackages();
                _installedPackages = NugetHelper.InstalledPackages.ToList();
                List<string> installedPackageNames = new List<string>();

                foreach (NugetPackage package in _installedPackages)
                {
                    if (!_expanded.ContainsKey(package))
                    {
                        _expanded.Add(package, false);
                    }
                    else
                    {
                        //Debug.LogErrorFormat("Expanded already contains {0} {1}", package.Id, package.Version);
                    }

                    installedPackageNames.Add(package.Id);
                }

                _installedPackageIds = installedPackageNames.ToArray();

                BuildTree();
            }
            catch (System.Exception e)
            {
                Debug.LogErrorFormat("{0}", e);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void BuildTree()
        {
            // default all packages to being roots
            _roots = new List<NugetPackage>(_installedPackages);

            // remove a package as a root if another package is dependent on it
            foreach (NugetPackage package in _installedPackages)
            {
                NugetFrameworkGroup frameworkGroup = NugetHelper.GetBestDependencyFrameworkGroupForCurrentSettings(package);
                foreach (NugetPackageIdentifier dependency in frameworkGroup.Dependencies)
                {
                    _roots.RemoveAll(p => p.Id == dependency.Id);
                }
            }
        }

        /// <summary>
        /// Automatically called by Unity to draw the GUI.
        /// </summary>
        protected void OnGUI()
        {
            _currentTab = GUILayout.Toolbar(_currentTab, _tabTitles);

            switch (_currentTab)
            {
                case 0:
                    _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
                    foreach (NugetPackage package in _roots)
                    {
                        DrawPackage(package);
                    }
                    EditorGUILayout.EndScrollView();
                    break;
                case 1:
                    EditorStyles.label.fontStyle = FontStyle.Bold;
                    EditorStyles.label.fontSize = 14;
                    EditorGUILayout.LabelField("Select Dependency:", GUILayout.Height(20));
                    EditorStyles.label.fontStyle = FontStyle.Normal;
                    EditorStyles.label.fontSize = 10;
                    EditorGUI.indentLevel++;
                    int newIndex = EditorGUILayout.Popup(_selectedPackageIndex, _installedPackageIds);
                    EditorGUI.indentLevel--;

                    if (newIndex != _selectedPackageIndex)
                    {
                        _selectedPackageIndex = newIndex;

                        _parentPackages.Clear();
                        NugetPackage selectedPackage = _installedPackages[_selectedPackageIndex];
                        foreach (var package in _installedPackages)
                        {
                            NugetFrameworkGroup frameworkGroup = NugetHelper.GetBestDependencyFrameworkGroupForCurrentSettings(package);
                            foreach (var dependency in frameworkGroup.Dependencies)
                            {
                                if (dependency.Id == selectedPackage.Id)
                                {
                                    _parentPackages.Add(package);
                                }
                            }
                        }
                    }
                    
                    EditorGUILayout.Space();
                    EditorStyles.label.fontStyle = FontStyle.Bold;
                    EditorStyles.label.fontSize = 14;
                    EditorGUILayout.LabelField("Packages That Depend on Above:", GUILayout.Height(20));
                    EditorStyles.label.fontStyle = FontStyle.Normal;
                    EditorStyles.label.fontSize = 10;

                    _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
                    EditorGUI.indentLevel++;
                    if (_parentPackages.Count <= 0)
                    {
                        EditorGUILayout.LabelField("NONE");
                    }
                    else
                    {
                        foreach (var parent in _parentPackages)
                        {
                            //EditorGUILayout.LabelField(string.Format("{0} {1}", parent.Id, parent.Version));
                            DrawPackage(parent);
                        }
                    }
                    EditorGUI.indentLevel--;
                    EditorGUILayout.EndScrollView();
                    break;
            } 
        }

        private void DrawDependency(NugetPackageIdentifier dependency)
        {
            NugetPackage fullDependency = _installedPackages.Find(p => p.Id == dependency.Id);
            if (fullDependency != null)
            {
                DrawPackage(fullDependency);
            }
            else
            {
                Debug.LogErrorFormat("{0} {1} is not installed!", dependency.Id, dependency.Version);
            }
        }

        private void DrawPackage(NugetPackage package)
        {
            if (package.Dependencies is {Count: > 0})
            {
                _expanded[package] = EditorGUILayout.Foldout(_expanded[package],
                    $"{package.Id} {package.Version}");

                if (_expanded[package])
                {
                    EditorGUI.indentLevel++;

                    NugetFrameworkGroup frameworkGroup = NugetHelper.GetBestDependencyFrameworkGroupForCurrentSettings(package);
                    foreach (NugetPackageIdentifier dependency in frameworkGroup.Dependencies)
                    {
                        DrawDependency(dependency);
                    }
                    EditorGUI.indentLevel--;
                }
            }
            else
            {
                EditorGUILayout.LabelField($"{package.Id} {package.Version}");
            }
        }
    }
}
