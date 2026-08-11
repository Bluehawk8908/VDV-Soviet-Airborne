using System.IO;
using System.Collections;
using MelonLoader;
using MelonLoader.Utils;
using GHPC;
using GHPC.State;
using GHPC.Infantry;
using UnityEngine;
using UnityEngine.Rendering;

namespace VDV
{
    public class VDVConverted : MonoBehaviour
    {
        void Awake()
        {
            enabled = false;
        }
    }

    public class VDV_Class : MelonMod
    {
        public static GameObject gameManager;
        public static MelonPreferences_Entry<bool> berets;
        public static MelonPreferences_Entry<bool> armpatches;
        public static MelonPreferences_Entry<bool> mute_logger;
        public static MelonPreferences_Entry<bool> rifle_bool;

        public override void OnInitializeMelon()
        {
            MelonPreferences_Category cfg = MelonPreferences.CreateCategory("VDV Soviet Airborne");
            berets = cfg.CreateEntry<bool>("Berets", true);
            berets.Comment = "Disable to restore steel helmets";

            armpatches = cfg.CreateEntry<bool>("Armpatches", true);
            armpatches.Comment = "Disable to hide VDV patches on sleeves";

            rifle_bool = cfg.CreateEntry<bool>("AKS-74s", true);
            rifle_bool.Comment = "Folding skeletal metal stocks replace solid wood";

            mute_logger = cfg.CreateEntry<bool>("Mute console Log", false);
            mute_logger.Comment = "Mutes log messages in the MelonLoader console.";
        }
        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (sceneName == "MainMenu2_Scene" || sceneName == "t64_menu" || sceneName == "MainMenu2-1_Scene")
            {
                return;
            }

            gameManager = GameObject.Find("_APP_GHPC_");
            if (gameManager == null) { return; }

            StateController.RunOrDefer(GameState.GameReady, new GameStateEventHandler(Conversion), GameStatePriority.Medium);
        }

        public IEnumerator Conversion(GameState _)
        {            
            InfantryUnit[] troops = GameObject.FindObjectsByType<InfantryUnit>(FindObjectsSortMode.None);
            foreach (var troop in troops)
            {               
                if (!troop.name.StartsWith("SA Obr73")) continue;
                if (troop.gameObject.GetComponent<VDVConverted>() != null) continue;                

                Transform upperLeftArm = troop.transform.Find("Troop Base/TRP_SKELETON/soldierHip/soldierSpine1/soldierSpine2/" +
                    "soldierSpine3/soldierChest/soldierLArmCollarbone/soldierLArm1").transform;
                Transform head = troop.transform.Find("Troop Base/TRP_SKELETON/soldierHip/soldierSpine1/soldierSpine2/" +
                    "soldierSpine3/soldierChest/soldierNeck1/soldierNeck2/soldierHead").transform;
                GameObject torso = troop.transform.Find("Troop Base/RED_OBR73_KHAKI/dress").gameObject;
                SkinnedMeshRenderer torso_smr = torso.GetComponent<SkinnedMeshRenderer>();
                BoneWeight[] boneWeights = torso_smr.sharedMesh.boneWeights;
                Matrix4x4[] bindPoses = torso_smr.sharedMesh.bindposes;
                Mesh originalMesh = torso_smr.sharedMesh;

                var vdv_bundle = AssetBundle.LoadFromFile(Path.Combine(MelonEnvironment.ModsDirectory + "/VDV", "vdv"));
                if (vdv_bundle == null) { 
                    MelonLogger.Error("Could not load test asset bundle");
                    continue;
                }
                
                if (armpatches.Value) {
                    GameObject armpatch = GameObject.Instantiate(vdv_bundle.LoadAsset("assets/armpatch.obj") as GameObject);
                    armpatch.transform.parent = upperLeftArm;
                    armpatch.transform.position = upperLeftArm.position;
                    armpatch.transform.localPosition = new Vector3(-0.17f, -0.05f, -0.035f);
                    armpatch.transform.localRotation = Quaternion.Euler(0f, 270f, 128f);
                    armpatch.transform.Find("default").GetComponent<MeshRenderer>().material.color = new Color(0.7f, 0.7f, 0.7f);
                }

                GameObject vdv_suit = GameObject.Instantiate(vdv_bundle.LoadAsset("assets/vdv_alt.obj") as GameObject);
                MeshFilter vdv_mf = vdv_suit.transform.Find("default").GetComponent<MeshFilter>();

                Texture2D vdv_suit_co = vdv_bundle.LoadAsset<Texture2D>("assets/VDV_co1.png");
                Texture2D vdv_suit_nm = vdv_bundle.LoadAsset<Texture2D>("assets/VDV_nm.png");
                Texture2D vdv_suit_ao = vdv_bundle.LoadAsset<Texture2D>("assets/VDV_ao.png");

                vdv_bundle.Unload(false);                
                
                vdv_mf.transform.parent = troop.transform;
                Vector3[] verts = vdv_mf.mesh.vertices;
                int[] tris = vdv_mf.mesh.triangles;
                Vector2[] uvs = vdv_mf.mesh.uv;

                Mesh newMesh = new Mesh();
                newMesh.indexFormat = originalMesh.indexFormat;

                GraphicsBuffer verticesBuffer = originalMesh.GetVertexBuffer(0);
                int vertTotalSize = verticesBuffer.stride * verticesBuffer.count;
                byte[] vertexData = new byte[vertTotalSize];
                verticesBuffer.GetData(vertexData);
                newMesh.SetVertexBufferParams(originalMesh.vertexCount, originalMesh.GetVertexAttributes());
                newMesh.SetVertexBufferData(vertexData, 0, 0, vertTotalSize);
                verticesBuffer.Release();                

                newMesh.subMeshCount = originalMesh.subMeshCount;
                GraphicsBuffer indicesBuffer = originalMesh.GetIndexBuffer();
                int indTotalSize = indicesBuffer.stride * indicesBuffer.count;
                byte[] indicesData = new byte[indTotalSize];
                indicesBuffer.GetData(indicesData);
                newMesh.SetIndexBufferParams(indicesBuffer.count, originalMesh.indexFormat);
                newMesh.SetIndexBufferData(indicesData, 0, 0, indTotalSize);
                indicesBuffer.Release();

                uint currentIndexOffset = 0;
                for (int i = 0; i < newMesh.subMeshCount; i++)
                {
                    uint subMeshIndexCount = originalMesh.GetIndexCount(i);
                    newMesh.SetSubMesh(i, new SubMeshDescriptor((int)currentIndexOffset, (int)subMeshIndexCount));
                    currentIndexOffset += subMeshIndexCount;
                }
                
                newMesh.vertices = verts;
                newMesh.triangles = tris;
                newMesh.uv = uvs;                
                newMesh.uv2 = new Vector2[0];
                newMesh.RecalculateNormals();
                newMesh.RecalculateBounds();
                newMesh.RecalculateTangents();

                torso_smr.sharedMesh = newMesh;                
                torso_smr.sharedMesh.boneWeights = boneWeights;
                torso_smr.sharedMesh.bindposes = bindPoses;
                torso_smr.material.SetTexture("_Albedo", vdv_suit_co);
                torso_smr.material.SetTexture("_Normal", vdv_suit_nm);
                torso_smr.material.SetTexture("_Occlusion", vdv_suit_ao);                                

                if (berets.Value) {
                    troop.transform.Find("Troop Base/RED_OBR73_KHAKI/helmet").gameObject.SetActive(false);
                    var beret_bundle = AssetBundle.LoadFromFile(Path.Combine(MelonEnvironment.ModsDirectory + "/VDV", "beret"));
                    if (beret_bundle == null) {
                        MelonLogger.Error("Could not load test asset bundle");
                        continue;
                    }
                    GameObject beret = GameObject.Instantiate(beret_bundle.LoadAsset("assets/VDV_beret.obj") as GameObject);
                    Texture2D beret_rm = beret_bundle.LoadAsset<Texture2D>("assets/vdv_beret_rm.png");
                    beret_bundle.Unload(false);
                    beret.transform.parent = head;
                    beret.transform.position = head.position;
                    MeshRenderer beret_mr = beret.transform.Find("default").GetComponent<MeshRenderer>();                
                    Material beret_mat = new Material(Shader.Find("GHPC/UniformShader"));
                    beret_mat.SetTexture("_Albedo", beret_mr.material.GetTexture("_MainTex"));
                    beret_mat.SetTexture("_Normal", beret_mr.material.GetTexture("_BumpMap"));
                    beret_mat.SetTexture("_Occlusion", beret_mr.material.GetTexture("_OcclusionMap"));
                    beret_mat.SetTexture("_Smoothness", beret_mr.material.GetTexture("_SpecGlossMap"));
                    beret_mat.SetTexture("_regions", beret_rm);
                    beret_mat.SetTexture("_PaintMask", torso_smr.material.GetTexture("_PaintMask"));
                    beret_mat.name = "beret";
                    beret_mr.material = beret_mat;
                
                    InfantryMaterialController imc = troop.transform.Find("Troop Base").GetComponent<InfantryMaterialController>();
                    GHPC.Utility.RendererMaterial beret_rendmat = new GHPC.Utility.RendererMaterial();
                    beret_rendmat.MaterialIndex = 0;
                    beret_rendmat.Renderer = beret_mr;
                    imc._bloodyRMaterials.Add(beret_rendmat);
                    //beret_mr.material.color = new Color(0.8f, 0.8f, 0.8f); material colour doesn't work with GHPC/UniformShader               

                    int seed = System.DateTime.Now.Millisecond; //berets have 40% of being straight, 40% cocked to the right, and 20% knocked back high on the forehead 
                    if (seed <= 399) { 
                        beret.transform.localRotation = Quaternion.Euler(new Vector3(270f, 90f, 0f));
                        beret.transform.localPosition = new Vector3(-0.135f, -0.01f, -0.008f);
                        beret.transform.localScale = new Vector3(1f, 1.05f, 1f);
                    }
                    else if (seed > 399 && seed <= 599)
                    {
                        beret.transform.localRotation = Quaternion.Euler(new Vector3(280f, 270f, 180f));
                        beret.transform.localPosition = new Vector3(-0.14f, -0.015f, -0.005f);
                    }
                    else
                    {
                        beret.transform.localRotation = Quaternion.Euler(new Vector3(280f, 255f, 180f));
                        beret.transform.localPosition = new Vector3(-0.135f, -0.01f, -0.02f);
                        beret.transform.localScale = new Vector3(1f, 1.05f, 1f);
                    }
                

                    AarVisual AarVis = troop.transform.Find("Troop Base").GetComponent<AarVisual>();
                    AarVis._renderers.Add(beret_mr);
                    AarVis.OriginalMaterials[torso_smr] = new System.Collections.Generic.List<Material> { torso_smr.material };
                    AarVis.OriginalMaterials.Add(beret_mr, new System.Collections.Generic.List<Material> { beret_mr.material });
                }

                if (rifle_bool.Value) { 
                    var rifle_bundle = AssetBundle.LoadFromFile(Path.Combine(MelonEnvironment.ModsDirectory + "/VDV", "aks74"));
                    if (rifle_bundle == null) { 
                        MelonLogger.Error("Could not load test asset bundle"); 
                        continue;
                    }
                    GameObject aks74 = GameObject.Instantiate(rifle_bundle.LoadAsset("assets/aks74.obj") as GameObject);
                    rifle_bundle.Unload(false);
                    GameObject default_rifle = troop.transform.Find("Troop Base/TRP_SKELETON/weaponmain/AK74").gameObject;
                
                    aks74.transform.parent = default_rifle.transform;
                    aks74.transform.position = default_rifle.transform.position;
                    aks74.transform.localPosition = new Vector3(-0.04f, 0.04f, 0f);
                    aks74.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
                    aks74.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
                
                    SkinnedMeshRenderer default_rifle_smr = default_rifle.transform.Find("AK74/ak74").GetComponent<SkinnedMeshRenderer>();
                    MeshRenderer new_rifle_mr = aks74.transform.Find("default").GetComponent<MeshRenderer>();
                    new_rifle_mr.material = default_rifle_smr.material;
                    default_rifle_smr.enabled = false;
                    troop.transform.Find("Troop Base/TRP_SKELETON/weaponmain/AK74/AK74 Rigidbody/WPN_AK74").GetComponent<MeshRenderer>().enabled = false;

                    AarVisual weapon_AAR = default_rifle.GetComponent<AarVisual>();
                    weapon_AAR._renderers = new System.Collections.Generic.List<Renderer> { new_rifle_mr };
                    weapon_AAR.OriginalMaterials = new System.Collections.Generic.Dictionary<Renderer, System.Collections.Generic.List<Material>> 
                        { [new_rifle_mr] = new System.Collections.Generic.List<Material>
                            {new_rifle_mr.material } 
                        };
                }

                if (!mute_logger.Value) MelonLogger.Msg(troop.name + " converted into airborne");
                troop.gameObject.AddComponent<VDVConverted>();
            }
            yield break;
        }
    }
}
