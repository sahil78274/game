using UnityEngine;

namespace NumberRush3D.Core
{
    /// <summary>
    /// Shader / material / font lookups that must work in a build without any authored assets.
    ///
    /// Everything here is cached: the runner track and the number gates build their geometry from
    /// primitives at runtime, and they must not fetch a shader or a font per object.
    /// </summary>
    public static class RuntimeAssets
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP / HDRP
        private static readonly int ColorId = Shader.PropertyToID("_Color");         // Built-in

        private static Shader _litShader;
        private static Font _font;
        private static bool _litShaderResolved;
        private static bool _fontResolved;

        /// <summary>URP Lit, then URP Simple Lit, then the built-in Standard shader.</summary>
        public static Shader LitShader
        {
            get
            {
                if (_litShaderResolved)
                {
                    return _litShader;
                }

                _litShaderResolved = true;
                _litShader = Shader.Find("Universal Render Pipeline/Lit");
                if (_litShader == null)
                {
                    _litShader = Shader.Find("Universal Render Pipeline/Simple Lit");
                }

                if (_litShader == null)
                {
                    _litShader = Shader.Find("Standard");
                }

                if (_litShader == null)
                {
                    _litShader = Shader.Find("Sprites/Default");
                }

                return _litShader;
            }
        }

        /// <summary>Built-in legacy font used by the 3D gate labels.</summary>
        public static Font LegacyFont
        {
            get
            {
                if (_fontResolved)
                {
                    return _font;
                }

                _fontResolved = true;

                try
                {
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                catch (System.Exception)
                {
                    _font = null;
                }

                if (_font == null)
                {
                    try
                    {
                        _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    }
                    catch (System.Exception)
                    {
                        _font = null;
                    }
                }

                return _font;
            }
        }

        /// <summary>Creates a shared lit material tinted with <paramref name="color"/>.</summary>
        public static Material CreateLitMaterial(Color color, string name, float smoothness = 0.12f)
        {
            Shader shader = LitShader;
            if (shader == null)
            {
                return null;
            }

            Material material = new Material(shader)
            {
                name = name,
                hideFlags = HideFlags.DontSave,
            };

            SetColor(material, color);

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", smoothness);
            }

            return material;
        }

        private static void SetColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else
            {
                material.color = color;
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }

        /// <summary>
        /// Tints one renderer through a shared <see cref="MaterialPropertyBlock"/>.
        /// Costs nothing per instance beyond the property block itself - no material cloning.
        /// </summary>
        public static void Tint(Renderer renderer, MaterialPropertyBlock block, Color color)
        {
            if (renderer == null || block == null)
            {
                return;
            }

            block.Clear();
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block);
        }
    }
}
