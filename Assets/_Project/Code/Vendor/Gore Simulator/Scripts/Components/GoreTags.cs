// ----------------------------------------------------
// Gore Simulator
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

namespace PampelGames.GoreSimulator
{
    /// <summary>
    ///     Attached to detached parts in the scene to help with identification.
    ///     Custom character and bone tags can be set in the Gore Simulator component.
    /// </summary>
    public class GoreTags : MonoBehaviour
    {
        public List<string> characterTags;
        public BoneTag boneTag;

        internal void SetCharacterTags(List<string> inputCharacterTags)
        {
            characterTags = inputCharacterTags;
        }

        internal void SetBoneTag(BoneTag inputBoneTag)
        {
            boneTag = inputBoneTag;
        }

        /********************************************************************************************************************************/

        /// <summary>
        ///     Checks if a custom character tag exists on this gameobject.
        /// </summary>
        public bool CharacterTagExists(string tagName)
        {
            return characterTags.Contains(tagName);
        }

        /// <summary>
        ///     Gets the list of character tags.
        /// </summary>
        /// <returns></returns>
        public List<string> GetCharacterTags()
        {
            return characterTags;
        }

        /// <summary>
        ///     Checks if a custom bone tag exists on this gameobject.
        /// </summary>
        public bool BoneTagExists(BoneTag _boneTag)
        {
            return boneTag == _boneTag;
        }

        /// <summary>
        ///     Returns the custom bone tag.
        /// </summary>
        public BoneTag GetBoneTag()
        {
            return boneTag;
        }
    }
}