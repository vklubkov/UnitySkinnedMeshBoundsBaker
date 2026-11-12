using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEngine.Debug;
using UnityObject = UnityEngine.Object;

namespace SkinnedMeshBoundsBaker {
	internal class SkinnedMeshBoundsBakerWindow : EditorWindow {
		[SerializeField] SkinnedMeshRenderer _meshRenderer;
		[SerializeField] Transform _rootBone;
		[SerializeField] float _paddingFactor = 1f;

		ObjectField _meshRendererField;
		ObjectField _rootBoneField;
		Slider _paddingFactorSlider;
		Button _calculateButton;

		[MenuItem("Tools/Skinned Mesh Bounds Baker")]
		public static void ShowWindow() {
			var window = CreateInstance<SkinnedMeshBoundsBakerWindow>();
			window.titleContent = new GUIContent("Skinned Mesh Bounds Baker");
			var fixedSize = new Vector2(360, 119);
			window.minSize = fixedSize;
			window.maxSize = fixedSize;
			window.Show();
		}

		public void CreateGUI() {
			var root = rootVisualElement;
			root.style.paddingTop = 10;
			root.style.paddingBottom = 10;
			root.style.paddingLeft = 10;
			root.style.paddingRight = 10;

			_meshRendererField = new ObjectField("Skinned Mesh");
			_meshRendererField.objectType = typeof(SkinnedMeshRenderer);
			_meshRendererField.allowSceneObjects = true;
			_meshRendererField.value = _meshRenderer;
			_meshRendererField.RegisterValueChangedCallback(OnMeshRendererChanged);
			root.Add(_meshRendererField);

			_rootBoneField = new ObjectField("New Root Bone Target (optional)");
			_rootBoneField.objectType = typeof(Transform);
			_rootBoneField.allowSceneObjects = true;
			_rootBoneField.value = _rootBone;
			_rootBoneField.RegisterValueChangedCallback(OnRootBoneChanged);
			root.Add(_rootBoneField);

			_paddingFactorSlider = new Slider("Extra Padding Factor", start: 1.0f, end: 1.5f);
			_paddingFactorSlider.value = _paddingFactor;
			_paddingFactorSlider.showInputField = true;
			_paddingFactorSlider.RegisterValueChangedCallback(OnPaddingFactorChanged);
			root.Add(_paddingFactorSlider);

			_calculateButton = new Button(RecalculateBounds);
			_calculateButton.text = "Bake Bounds";
			_calculateButton.style.height = 35;
			_calculateButton.style.unityFontStyleAndWeight = FontStyle.Bold;
			_calculateButton.SetEnabled(false);
			root.Add(_calculateButton);
		}

		void OnMeshRendererChanged(ChangeEvent<UnityObject> evt) {
			_meshRenderer = evt.newValue as SkinnedMeshRenderer;
			_calculateButton.SetEnabled(_meshRenderer != null);
		}

		void OnRootBoneChanged(ChangeEvent<UnityObject> evt) => _rootBone = evt.newValue as Transform;
		void OnPaddingFactorChanged(ChangeEvent<float> evt) => _paddingFactor = evt.newValue;

		void RecalculateBounds() {
			var skinnedMeshRenderer = _meshRendererField.value as SkinnedMeshRenderer;
			if (skinnedMeshRenderer == null) {
				LogError("[Skinned Mesh Bounds Baker]: Skinned mesh renderer not assigned!");
				return;
			}

			var newRootBone = _rootBoneField.value as Transform;

			Undo.RecordObject(skinnedMeshRenderer, "Bake Bounds");

			if (newRootBone != null)
				skinnedMeshRenderer.rootBone = newRootBone;

			var rootBone = skinnedMeshRenderer.rootBone;
			if (rootBone == null) {
				LogError("[Skinned Mesh Bounds Baker]: No root bone assigned or found!");
				return;
			}

			var snapshotMesh = new Mesh();
			try {
				skinnedMeshRenderer.BakeMesh(snapshotMesh, useScale: true);
				var vertices = snapshotMesh.vertices;
				if (vertices.Length == 0) {
					LogError("[Skinned Mesh Bounds Baker]: Mesh has no vertices!");
					return;
				}

				var minPoints = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
				var maxPoints = new Vector3(float.MinValue, float.MinValue, float.MinValue);

				// Matrix to convert from the skinned mesh renderer local space
				// to the world space to the target root bone local space
				var skinnedMeshRendererToRootBoneMatrix =
					rootBone.worldToLocalMatrix * skinnedMeshRenderer.transform.localToWorldMatrix;

				foreach (var point in vertices) {
					var skinnedMeshRendererToRootBonePoint = skinnedMeshRendererToRootBoneMatrix.MultiplyPoint3x4(point);
					minPoints = Vector3.Min(minPoints, skinnedMeshRendererToRootBonePoint);
					maxPoints = Vector3.Max(maxPoints, skinnedMeshRendererToRootBonePoint);
				}

				var localCenter = (minPoints + maxPoints) * 0.5f;
				var localSize = (maxPoints - minPoints) * _paddingFactorSlider.value;
				skinnedMeshRenderer.localBounds = new Bounds(localCenter, localSize);
				EditorUtility.SetDirty(skinnedMeshRenderer);

				Log($"[Skinned Mesh Bounds Baker]: Bounds baked for '{rootBone.name}'! " +
					$"Center: {localCenter}, Size: {localSize}", skinnedMeshRenderer);
			}
			finally {
				DestroyImmediate(snapshotMesh);
			}
		}
	}
}
