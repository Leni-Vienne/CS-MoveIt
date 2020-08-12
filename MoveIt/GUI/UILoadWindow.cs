﻿using ColossalFramework;
using ColossalFramework.UI;
using MoveIt.Localization;
using System.IO;
using UIUtils = SamsamTS.UIUtils;
using UnityEngine;

namespace MoveIt
{
    public class UILoadWindow : UIPanel
    {
        public static readonly SavedInt loadWindowX = new SavedInt("loadWindowX", MoveItTool.settingsFileName, -1000, true);
        public static readonly SavedInt loadWindowY = new SavedInt("loadWindowY", MoveItTool.settingsFileName, -1000, true);

        public class UIFastList : UIFastList<string, UISaveLoadFileRow> { }
        public UIFastList fastList;

        public UIButton close;

        public static UILoadWindow instance;

        public override void Start()
        {
            name = "MoveIt_SaveWindow";
            atlas = UIUtils.GetAtlas("Ingame");
            backgroundSprite = "SubcategoriesPanel";
            size = new Vector2(545, 272);
            canFocus = true;

            UIDragHandle dragHandle = AddUIComponent<UIDragHandle>();
            dragHandle.target = parent;
            dragHandle.relativePosition = Vector3.zero;

            close = AddUIComponent<UIButton>();
            close.size = new Vector2(30f, 30f);
            close.text = "X";
            close.textScale = 0.9f;
            close.textColor = new Color32(118, 123, 123, 255);
            close.focusedTextColor = new Color32(118, 123, 123, 255);
            close.hoveredTextColor = new Color32(140, 142, 142, 255);
            close.pressedTextColor = new Color32(99, 102, 102, 102);
            close.textPadding = new RectOffset(8, 8, 8, 8);
            close.canFocus = false;
            close.playAudioEvents = true;
            close.relativePosition = new Vector3(width - close.width, 0);

            close.eventClicked += (c, p) =>
            {
                Close();
            };

            UILabel label = AddUIComponent<UILabel>();
            label.textScale = 0.9f;
            label.text = Str.xml_Import;
            label.relativePosition = new Vector2(8, 8);
            label.SendToBack();

            // FastList
            fastList = AddUIComponent<UIFastList>();
            fastList.backgroundSprite = "UnlockingPanel";
            fastList.width = width - 16;
            fastList.height = 46 * 7;
            fastList.canSelect = true;
            fastList.relativePosition = new Vector3(8, 28);

            fastList.rowHeight = 46f;

            height = fastList.relativePosition.y + fastList.height + 8;
            dragHandle.size = size;
            absolutePosition = new Vector3(loadWindowX.value, loadWindowY.value);
            MakePixelPerfect();

            RefreshFileList();

            UIComponent modalEffect = GetUIView().panelsLibraryModalEffect;
            if (modalEffect != null && !modalEffect.isVisible)
            {
                modalEffect.Show(false);
                ValueAnimator.Animate("ModalEffect", delegate (float val)
                {
                    modalEffect.opacity = val;
                }, new AnimatedFloat(0f, 1f, 0.7f, EasingType.CubicEaseOut));
            }

            BringToFront();
            Focus();
        }

        public static void Open()
        {
            if (instance == null)
            {
                instance = UIView.GetAView().AddUIComponent(typeof(UILoadWindow)) as UILoadWindow;
                UIView.PushModal(instance);
            }
        }

        public static void Close()
        {
            if (instance != null)
            {
                UIView.PopModal();

                UIComponent modalEffect = instance.GetUIView().panelsLibraryModalEffect;
                if (modalEffect != null && modalEffect.isVisible)
                {
                    modalEffect.Hide();

                    /*ValueAnimator.Animate("ModalEffect", delegate (float val)
                    {
                        modalEffect.opacity = val;
                    }, new AnimatedFloat(1f, 0f, 0.7f, EasingType.CubicEaseOut), delegate
                    {
                        modalEffect.Hide();
                    });*/
                }

                instance.isVisible = false;
                Destroy(instance.gameObject);
                instance = null;
            }
        }

        protected override void OnKeyDown(UIKeyEventParameter p)
        {
            if (Input.GetKey(KeyCode.Escape))
            {
                p.Use();
                Close();
            }

            base.OnKeyDown(p);
        }

        protected override void OnPositionChanged()
        {
            Vector2 resolution = GetUIView().GetScreenResolution();

            if (absolutePosition.x == -1000)
            {
                absolutePosition = new Vector2((resolution.x - width) / 2, (resolution.y - height) / 2);
                MakePixelPerfect();
            }

            absolutePosition = new Vector2(
                (int)Mathf.Clamp(absolutePosition.x, 0, resolution.x - width),
                (int)Mathf.Clamp(absolutePosition.y, 0, resolution.y - height));

            loadWindowX.value = (int)absolutePosition.x;
            loadWindowY.value = (int)absolutePosition.y;

            base.OnPositionChanged();
        }

        public void RefreshFileList()
        {
            fastList.rowsData.Clear();

            if (Directory.Exists(MoveItTool.saveFolder))
            {
                string[] files = Directory.GetFiles(MoveItTool.saveFolder, "*.xml");

                foreach (string file in files)
                {
                    fastList.rowsData.Add(Path.GetFileNameWithoutExtension(file));
                }

                fastList.DisplayAt(0);
            }

            Focus();
        }
    }
}
