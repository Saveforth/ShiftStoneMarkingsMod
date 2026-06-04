using Il2CppRUMBLE.Combat.ShiftStones;
using Il2CppRUMBLE.Input;
using Il2CppRUMBLE.Managers;
using Il2CppRUMBLE.Players;
using Il2CppRUMBLE.Players.Subsystems;
using Il2CppRUMBLE.Poses;
using Il2CppTMPro;
using MelonLoader;
using MelonLoader.Utils;
using System.Collections;
using UIFramework;
using UnityEngine;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Rendering.Universal;
using static MelonLoader.MelonLogger;

[assembly: MelonInfo(typeof(ShiftStoneMarkings.Core), "ShiftStoneMarkings", "1.0.0", "SaveForth", null)]
[assembly: MelonGame("Buckethead Entertainment", "RUMBLE")]
[assembly: MelonAdditionalDependencies("UIFramework")]

namespace ShiftStoneMarkings
{
    public class Core : MelonMod
    {

        private MelonPreferences_Category options;
        private MelonPreferences_Category extraoptions;


        private const string USER_DATA = "UserData/ShiftStoneMarkings/";
        private const string CONFIG_FILE = "config.cfg";
        private MelonPreferences_Entry<bool> toggle;
        private MelonPreferences_Entry<float> glowStren;
        private MelonPreferences_Entry<bool> isFeminineCharacter;
        private MelonPreferences_Entry<bool> toggleBasicColors;
        public static PlayerShiftstoneSystem localShiftstoneSystem;

        bool isSetup =false;
        Material mat;
        Texture2D editable;
        bool callback = false;
        Color[] pixels;
        List<BasicColor> myColors;
        Texture source;
        int progress = 0;
        int total = 100;
        AssetBundle shaderBundle;
        Shader glowShader;
        List<BasicColor> outputColors;
        float pulseAmountLeft = 0;
        float pulseAmountRight = 0;
        static SkinnedMeshRenderer overlayLeft;
        static SkinnedMeshRenderer overlayRight;
        static bool isLeftPulsing = false;
        static bool isRightPulsing = false;
        static byte alpha = 255;
        static bool isFeminine = true;
        static float glowStrength =10f;//Put this in the settings
        //static Color[] shiftStoneColors = { new Color32(205,48,251,alpha),new Color32(253,255,103, alpha), new Color32(69, 185, 164, alpha), new Color32(47,147,241, alpha), new Color32(46,145,239, alpha), new Color32(59,173,86, alpha), new Color32(215, 254, 70, alpha), new Color32(206, 42, 66, alpha) };
        static Color[] shiftStoneColors =
                            {
                                new Color32(190,   0, 255, alpha), // purple
                                new Color32(255, 210,   0, alpha), // yellow/gold
                                new Color32(  0, 210, 170, alpha), // teal
                                new Color32(255, 100,   0, alpha), // orange
                                new Color32(  0, 100, 255, alpha), // blue
                                new Color32(  0, 220,  70, alpha), // green
                                new Color32(170, 255,   0, alpha), // lime
                                new Color32(255,   0,  45, alpha), // red
                            };

                            


        public override void OnInitializeMelon()
        {
            options = MelonPreferences.CreateCategory("options", "Options");
            extraoptions = MelonPreferences.CreateCategory("extra", "Extra");
            if (!Directory.Exists(USER_DATA))
            {
                Directory.CreateDirectory(USER_DATA);
            }
            options.SetFilePath(Path.Combine(USER_DATA, CONFIG_FILE));
            

            toggle = options.CreateEntry("Entry 1-1", true, "ToggleMod", "Turn the mod on or off");
            glowStren = options.CreateEntry<float>("Entry 1-2", 5f, "Glow Strength", "Changes the strength of the glow (too high might become white)");
            isFeminineCharacter = options.CreateEntry("Entry 1-3", true, "IsCharacterFeminine", "If using feminine character model, this needs to be on");
            toggleBasicColors = extraoptions.CreateEntry("Entry 2-1", false, "Basic Colors", "Toggle basic colors (allows brighter glow typically)");

            toggle.OnEntryValueChanged.Subscribe((oldVal, newVal) => toggleMod(newVal));
            glowStren.OnEntryValueChanged.Subscribe((oldVal, newVal) => { 
                overlayLeft.material.SetFloat("_GlowStrength", newVal);
                overlayRight.material.SetFloat("_GlowStrength", newVal);
                glowStrength = newVal;
            });
            isFeminineCharacter.OnEntryValueChanged.Subscribe((oldVal, newVal) => { isFeminine = newVal; MelonCoroutines.Start(waitForLoad()); });
            toggleBasicColors.OnEntryValueChanged.Subscribe((oldVal, newVal) => toggleColorsBasic(newVal));

            UI.RegisterMelon(this, [options,extraoptions]);

        }

        IEnumerator waitForLoad()
        {
            yield return new WaitForSeconds(4);
            maskSetup();
            shaderSetup();
        }

        public override void OnSceneWasLoaded(int buildindex, String buildname)
        {
            isSetup = false;
            if (buildindex != 0)
            {
                
                MelonCoroutines.Start(waitForLoad());
            }
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            if (isLeftPulsing)
            {
                BoolAndFloat pulseInfo = doPulse(overlayLeft,pulseAmountLeft,isLeftPulsing);
                pulseAmountLeft = pulseInfo.floatValue;
                isLeftPulsing = pulseInfo.boolValue;
            }
            if (isRightPulsing)
            {
                BoolAndFloat pulseInfo = doPulse(overlayRight, pulseAmountRight, isRightPulsing);
                pulseAmountRight = pulseInfo.floatValue;
                isRightPulsing = pulseInfo.boolValue;
            }
            if (Input.GetKeyDown(KeyCode.N))
            {
                isLeftPulsing = true;
            }
            if (Input.GetKeyDown(KeyCode.M))
            {
                isRightPulsing = true;
            }
            if (callback)
            {
                callback = false;

                for (int i = 0; i < pixels.Length; i++)//UnConvert from BasicColor back to unity color
                {
                    pixels[i] = new Color(outputColors[i].r, outputColors[i].g, outputColors[i].b, 1);//Make it a mask
                }

                editable.SetPixels(pixels);
                editable.Apply();
                MelonLogger.Msg("done");
                
                isSetup= true;
            }
            
        }
        //byte[] png = editable.EncodeToPNG();//print mask to png

        //File.WriteAllBytes(
        //    Path.Combine(
        //        MelonEnvironment.UserDataDirectory,
        //        "MarkingsMask.png"
        //    ),
        //    png
        //);
        void maskSetup()
        {
            mat = PlayerManager.instance.localPlayer.Controller.transform.GetChild(1).GetChild(0).GetComponent<SkinnedMeshRenderer>().material;
            source = mat.GetTexture("_ColorAtlas");
            editable = CopyReadable(source);

            BasicColor newMarkingColor = new() { r = 1, g = 0, b = 0, a = 1 };
            pixels = editable.GetPixels();
            StreamReader reader = new StreamReader("UserData/ShiftStoneMarkings/InitialMask.txt");
            List<List<int>> initMask = new List<List<int>>();
            while (!reader.EndOfStream)//Read in mask of player texture to only skin areas.
            {
                string line = reader.ReadLine();
                string[] strings = line.Split(' ');
                initMask.Add([int.Parse(strings[0]), int.Parse(strings[1])]);
            }

            myColors = new List<BasicColor>();//Convert to non-unity dependent list for threading.
            for (int i = 0; i < pixels.Length; i++)
            {
                myColors.Add(new() { r = pixels[i].r, g = pixels[i].g, b = pixels[i].b, a = pixels[i].a });
            }
            BasicColor black = new BasicColor() { r=0,g=0,b=0,a=1 };
            outputColors = new List<BasicColor>(myColors.Count);
            for (int i = 0; i < myColors.Count; i++)
            {
                outputColors.Add(black);
            }

            new Thread(() =>
            {//Create mask of only markings.
                for (int i = 0; i < myColors.Count; i++)
                {
                    outputColors.Add(black);
                }
                total = myColors.Count;

                for(int i = 0; i < initMask.Count; i++)
                {
                    for(int j = initMask[i][0]; j < initMask[i][1]; j++)
                    {
                        if (!isFeminine)
                        {
                            if (!myColors[j].equals(myColors[85]))//Check if pixel is skin.
                            {
                                outputColors[j] = newMarkingColor;
                            }
                        }
                        else
                        {
                            if (!myColors[j].similar(myColors[85], 0.14f))//Check if pixel is skin.
                            {
                                outputColors[j] = newMarkingColor;
                            }
                        }
                        
                        
                    }
                    progress = i;
                }
                
                callback = true;
            }).Start();
        }





        Texture2D CopyReadable(Texture source)
        {
            RenderTexture rt = RenderTexture.GetTemporary(
                source.width,
                source.height,
                0,
                RenderTextureFormat.Default,
                RenderTextureReadWrite.sRGB
            );

            Graphics.Blit(source, rt);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;

            Texture2D readable = new Texture2D(
                source.width,
                source.height,
                TextureFormat.RGBA32,
                false,
                false
            );

            readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            readable.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            return readable;
        }

        public struct BasicColor()
        {
            public float r;
            public float g;
            public float b;
            public float a;

            public bool equals(BasicColor other)
            {
                if(r==other.r && g==other.g && b == other.b)
                {
                    return true;
                }
                else
                {
                    return false;
                }
                
            }

            public void setA(float a) {
                this.a = a;
            }

            public bool similar(BasicColor other, float tolerance)
            {
                return Math.Abs(r - other.r) <= tolerance &&
                       Math.Abs(g - other.g) <= tolerance &&
                       Math.Abs(b - other.b) <= tolerance;
            }
        }

        void shaderSetup()
        {

            glowShader = RumbleModdingAPI.RMAPI.AssetBundles.LoadAssetFromStream<Shader>(this, "ShiftStoneMarkings.GlowShader.glowshaderbundle", "gloweffect");


            SkinnedMeshRenderer original = PlayerManager.instance.localPlayer.Controller.transform.GetChild(1).GetChild(0).GetComponent<SkinnedMeshRenderer>();

            GameObject overlayLeftObject = new GameObject("MarkingGlowOverlayLeft");
            overlayLeftObject.transform.SetParent(original.transform, false);
            GameObject overlayRightObject = new GameObject("MarkingGlowOverlayRight");
            overlayRightObject.transform.SetParent(original.transform, false);
            
            overlayLeft = overlayLeftObject.AddComponent<SkinnedMeshRenderer>();
            overlayRight = overlayRightObject.AddComponent<SkinnedMeshRenderer>();

            materialSetup(overlayLeft, original, Color.red);

            materialSetup(overlayRight, original, Color.blue);

            setStoneColors();
        }
        private void materialSetup(SkinnedMeshRenderer overlay, SkinnedMeshRenderer original, Color color)
        {
            overlay.sharedMesh = original.sharedMesh;
            overlay.bones = original.bones;
            overlay.rootBone = original.rootBone;
            overlay.material = new Material(glowShader);
            overlay.material.SetColor("_EmissionColor", color);
            overlay.material.SetFloat("PulseSpeed", 2f);
            overlay.material.SetFloat("_GlowStrength", glowStrength);
            overlay.material.SetTexture("_ColorAtlas", editable);
            overlay.material.SetFloat("_PulseAmount", -((float)Math.PI) / 2);
            overlay.material.SetFloat("_Offset", 0.005f);
            overlay.material.SetFloat("_Toggle", 0);
        }


        BoolAndFloat doPulse(SkinnedMeshRenderer overlay, float pulseAmount, bool isPulsing)
        {
            if (isSetup)
            {
                overlay.material.SetFloat("_Toggle", 1);
                overlay.material.SetFloat("_PulseAmount", pulseAmount);
                pulseAmount += .1f;
                if (pulseAmount > ((3 * (float)Math.PI) / 2))
                {
                    pulseAmount = (-((float)Math.PI) / 2);
                    isPulsing = false;
                    overlay.material.SetFloat("_Toggle", 0);
                }
            }
            return new() { boolValue= isPulsing, floatValue= pulseAmount};
            
        }



        public static void setStoneColors()
        {
            if (PlayerManager.instance.localPlayer.Controller != null &&overlayLeft!=null&&overlayRight!=null)
            {
                int[] colors = PlayerManager.instance.localPlayer.Controller.GetComponent<PlayerShiftstoneSystem>().GetCurrentShiftStoneConfiguration();
                overlayLeft.material.SetColor("_EmissionColor", shiftStoneColors[colors[0]]);
                overlayRight.material.SetColor("_EmissionColor", shiftStoneColors[colors[1]]);
            }
            
        }


        public void toggleMod(bool isOn)
        {
            if (isOn)
            {
                overlayLeft.gameObject.SetActive(true);
                overlayRight.gameObject.SetActive(true);
            }
            else
            {
                overlayLeft.gameObject.SetActive(false);
                overlayRight.gameObject.SetActive(false);
            }
        }


        void toggleColorsBasic(bool newVal)
        {
            if (newVal)
            {
                shiftStoneColors = [
                                Color.purple, // purple
                                Color.yellow, // yellow/gold
                                Color.teal, // teal
                                Color.orange, // orange
                                Color.blue, // blue
                                Color.green, // green
                                Color.limeGreen, // lime
                                Color.red, // red
                            ];
            }
            else
            {
                shiftStoneColors = [
                                new Color32(190, 0, 255, alpha), // purple
                                new Color32(255, 210, 0, alpha), // yellow/gold
                                new Color32(0, 210, 170, alpha), // teal
                                new Color32(255, 100, 0, alpha), // orange
                                new Color32(0, 100, 255, alpha), // blue
                                new Color32(0, 220, 70, alpha), // green
                                new Color32(170, 255, 0, alpha), // lime
                                new Color32(255, 0, 45, alpha), // red
                            ]
                ;
            }
            setStoneColors();
        }

        struct BoolAndFloat
        {
            public bool boolValue;
            public float floatValue;
        }


        [HarmonyLib.HarmonyPatch(typeof(PlayerShiftstoneSystem), "ActivateUseShiftstoneEffects", new Type[] { typeof(InputManager.Hand) })]
        public static class shiftStoneActivationPatch
        {

            private static void Postfix(PlayerShiftstoneSystem __instance, InputManager.Hand hand)
            {
                if (Core.localShiftstoneSystem == null)
                {
                    Core.localShiftstoneSystem = PlayerManager.instance.localPlayer.Controller.GetComponent<PlayerShiftstoneSystem>();
                }
                
                if(__instance == Core.localShiftstoneSystem)
                {
                    if (hand == InputManager.Hand.Left)
                    {
                        isLeftPulsing = true;
                    }
                    else if (hand == InputManager.Hand.Right)
                    {
                        isRightPulsing = true;
                    }
                }
                
                
            }
        }



        [HarmonyLib.HarmonyPatch(typeof(PlayerShiftstoneSystem), "AttachShiftStone", new Type[] { typeof(ShiftStone), typeof(int), typeof(bool), typeof(bool) })]
        public static class shiftStoneAttachPatch
        {

            private static void Postfix(ShiftStone stone, int slotIndex, bool saveInSettings, bool syncWithOtherPlayers)
            {
                setStoneColors();
            }
        }


    }
}