// ----------------------------------------------------
// Gore Simulator
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using PampelGames.Shared.Editor;
using PampelGames.Shared.Utility;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PampelGames.GoreSimulator.Editor
{
    [CustomEditor(typeof(GorePuppetSetup))]  
    public class GorePuppetSetupInspector : UnityEditor.Editor  
    {  
        private GorePuppetSetup _gorePuppetSetup;

        private VisualElement container;  

        private ObjectField mainCharacter;
        private ObjectField puppet;

        private Button findGoreBone;
        
        protected void OnEnable()  
        {         
            _gorePuppetSetup = target as GorePuppetSetup;
            container = new VisualElement();  
            
            mainCharacter = new ObjectField("Main Character");
            mainCharacter.objectType = typeof(GameObject);
            mainCharacter.tooltip = "The main character, initialized with Gore Simulator.";
            
            puppet = new ObjectField("Puppet");
            puppet.objectType = typeof(GameObject);
            puppet.tooltip = "The puppet, created by Puppet Master.";
            
            findGoreBone = new Button();
            findGoreBone.text = "Setup";
            findGoreBone.tooltip =
                "Attaches the 'GorePuppet' script to all puppet bones and assigned the associated 'GoreBone' from the main character.";
            
            BindElements();
        }

        private void BindElements()
        {
            mainCharacter.PGSetupBindProperty(serializedObject, nameof(_gorePuppetSetup.mainCharacter));
            puppet.PGSetupBindProperty(serializedObject, nameof(_gorePuppetSetup.puppet));
        }
        
        /********************************************************************************************************************************/
        
        public override VisualElement CreateInspectorGUI()  
        {  
            mainCharacter.tooltip = "The associated bone on the main character.";

            findGoreBone.clicked += () =>
            {
                var goreBones = _gorePuppetSetup.mainCharacter.transform.GetComponentsInChildren<GoreBone>();
                var configurableJonits = _gorePuppetSetup.puppet.GetComponentsInChildren<ConfigurableJoint>(); 

                foreach (var joint in configurableJonits)
                {
                    foreach (var goreBone in goreBones)
                    {
                        if (joint.gameObject.name != goreBone.gameObject.name) continue;
                        var gorePuppet = joint.gameObject.GetComponent<GorePuppet>();
                        if (gorePuppet == null) gorePuppet = joint.gameObject.AddComponent<GorePuppet>();
                        gorePuppet.goreBone = goreBone;
                        EditorUtility.SetDirty(gorePuppet);
                        break;
                    }
                }
            };
            
            container.Add(mainCharacter);
            container.Add(puppet);
            container.Add(findGoreBone);
            return container;  
        }
        
    }
}