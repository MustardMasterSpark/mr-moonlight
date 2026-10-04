using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace HeurekaGames.SmartBuilder
{
    //This class contains an implementation for string comparison algorithm
    //based on character pair similarity
    //Source: http://www.catalysoft.com/articles/StrikeAMatch.html
    public static class SB_Utils
    {
        public static bool TryRemoveObsoleteGUIDs(this List<string> entries)
        {
            //Remove any guid which no longer exist in db
            var tmp = entries.Where(x => AssetDatabase.LoadMainAssetAtPath((AssetDatabase.GUIDToAssetPath(x))) != null).ToList();
            bool didDelete = !(tmp.All(entries.Contains) && tmp.Count == entries.Count);
            entries = tmp;

            return (didDelete);
        }

        //Get the subset of tags, that is not ignored
        public static string[] RemoveIgnored(string[] tags, string[] ignored)
        {
            return tags.Where(x => !ignored.Contains(x)).Select(x => x.ToLower()).ToArray();
        }

        public static string[] GetFilenameSplit(string filename)
        {
            //TODO optimize this part AND/OR allow preferences to choose which ways we want to extracts tags from filename
            //filename = filename.ToLower();
            var sizeMatch = Regex.Match(filename.ToLower(), @"\d+x\d+"); //Taking care of tags such as 100x100 64x64 etc
            if (sizeMatch.Success)
                filename = filename.Replace(sizeMatch.Value, "");

            var tags = Regex.Split(filename, @" |-|_|-\.|[()]");//Regex split by delimiters ('-','_','.','()' numbers and space)//|D+
            var tmpTags = tags.SelectMany(x => Regex.Split(x, @"(?=[A-Z][^A-Z])"));//Regex split by hungarian notation

            tmpTags = tmpTags.SelectMany(x => Regex.Split(x, @"(\d+)"));//Split out numbers
            tmpTags = tmpTags.Where(x => !int.TryParse(x, out _)); //Remove numbers from search
            var newTagsList = tmpTags.ToList();

            //If we indeed found a '100x100' or similar add it back to the array of tags
            if (sizeMatch.Success)
                newTagsList.Add(sizeMatch.Value); //Add the specialcase 100x100 etc as found in the above

            return newTagsList.Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList().ConvertAll(x => x.ToLowerInvariant()).ToArray(); //Remove empty elements, make lowercase and take only unique values

        }

        /// <summary>
        /// Compares the two strings based on letter pair matches
        /// </summary>
        /// <param name="str1"></param>
        /// <param name="str2"></param>
        /// <returns>The percentage match from 0.0 to 1.0 where 1.0 is 100%</returns>
        public static float GetStringSimilarity(string str1, string str2)
        {
            List<string> pairs1 = WordLetterPairs(str1.ToUpper());
            List<string> pairs2 = WordLetterPairs(str2.ToUpper());

            int intersection = 0;
            int union = pairs1.Count + pairs2.Count;

            for (int i = 0; i < pairs1.Count; i++)
            {
                for (int j = 0; j < pairs2.Count; j++)
                {
                    if (pairs1[i] == pairs2[j])
                    {
                        intersection++;
                        pairs2.RemoveAt(j);//Must remove the match to prevent "GGGG" from appearing to match "GG" with 100% success

                        break;
                    }
                }
            }

            var value = (float)(2.0 * intersection) / union;
            return float.IsNaN(value) ? 0 : value;
        }

        /// <summary>
        /// Gets all letter pairs for each
        /// individual word in the string
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private static List<string> WordLetterPairs(string str)
        {
            List<string> AllPairs = new List<string>();

            // Tokenize the string and put the tokens/words into an array
            string[] Words = Regex.Split(str, @"\s");

            // For each word
            for (int w = 0; w < Words.Length; w++)
            {
                if (!string.IsNullOrEmpty(Words[w]))
                {
                    // Find the pairs of characters
                    String[] PairsInWord = LetterPairs(Words[w]);

                    for (int p = 0; p < PairsInWord.Length; p++)
                    {
                        AllPairs.Add(PairsInWord[p]);
                    }
                }
            }

            return AllPairs;
        }

        /// <summary>
        /// Generates an array containing every 
        /// two consecutive letters in the input string
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private static string[] LetterPairs(string str)
        {
            int numPairs = str.Length - 1;

            string[] pairs = new string[numPairs];

            for (int i = 0; i < numPairs; i++)
            {
                pairs[i] = str.Substring(i, 2);
            }

            return pairs;
        }

        public static Bounds GetGameobjectBounds(GameObject go)
        {
            var referenceTransform = go.transform;
            var b = new Bounds(Vector3.zero, Vector3.zero);
            RecurseEncapsulate(referenceTransform, ref b);
            return b;

            void RecurseEncapsulate(Transform child, ref Bounds bounds)
            {
                var filter = child.GetComponent<MeshFilter>();
                if (filter)
                {
                    if(filter.sharedMesh!=null)
                    {
                        var lsBounds = filter.sharedMesh.bounds;
                        var wsMin = child.TransformPoint(lsBounds.center - lsBounds.extents);
                        var wsMax = child.TransformPoint(lsBounds.center + lsBounds.extents);
                        bounds.Encapsulate(referenceTransform.InverseTransformPoint(wsMin));
                        bounds.Encapsulate(referenceTransform.InverseTransformPoint(wsMax));
                    }
                }
                foreach (Transform grandChild in child.transform)
                {
                    RecurseEncapsulate(grandChild, ref bounds);
                }
            }
        }
    }
}