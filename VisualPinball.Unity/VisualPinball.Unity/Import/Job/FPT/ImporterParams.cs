// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.


////////////////////////////////////////////////////////////////////////////
// This class will handle a generic list of imported parameters in url style

using System.Collections.Generic;

namespace VisualPinball.Unity.FP
{
    public class ImporterParams //: MonoBehaviour
    {
        protected Dictionary<string, string> m_params = new Dictionary<string, string>();

        // used to save between sessions as Dictionary is not serializable
        public string[] m_keys;
        public string[] m_values;
        // Serialization reading
        //void Awake()
        public ImporterParams()
        {
            if (m_keys != null && m_values != null)
                for (int i = 0; i < m_keys.Length; i++)
                    m_params.Add(m_keys[i], m_values[i]);
        }

        // parse a string as Url and return the base string
        public string Parse(string input)
        {
            string[] tmp = input.Split('?');
            if (tmp.Length <= 1)
                return input; // No arguments

            string[] args = tmp[1].Split('&');

            foreach (string s in args)
            {
                string[] a = s.Split('=');
                string param = a[0];
                string val = a.Length > 1 ? a[1] : "True";
                //Debug.LogError ("Parsed param for "+tmp[0]+": "+param + "=" + val);
                if (!m_params.ContainsKey(param))
                    m_params.Add(param, val);
                else
                    m_params[param] = val; // Only the last declaration of this argument will remain
                                           //Debug.LogError ("ToInt:"+GetInt (param,-1));
                                           //Debug.LogError ("ToFloat:"+GetFloat (param,-1f));
                                           //Debug.LogError ("ToBool:"+GetBool (param,false));
            }

            // Serialization hack
            {
                m_keys = new string[m_params.Keys.Count];
                m_values = new string[m_params.Values.Count];
                {
                    int i = 0;
                    foreach (string s in m_params.Keys)
                        m_keys[i++] = s;
                }
                {
                    int i = 0;
                    foreach (string s in m_params.Values)
                        m_values[i++] = s;
                }
            }

            return tmp[0];
        }

        // Utils functions
        public bool Contains(string key)
        {
            return m_params.ContainsKey(key);
        }

        public string Get(string key, string defaultValue = "")
        {
            return GetString(key, defaultValue);
        }

        public string GetString(string key, string defaultValue = "")
        {
            if (!m_params.ContainsKey(key))
                return defaultValue;
            return m_params[key];
        }

        public int GetInt(string key, int defaultValue = 0)
        {
            if (!m_params.ContainsKey(key))
                return defaultValue;

            int val = defaultValue;
            return int.TryParse(m_params[key], out val) ? val : defaultValue;
        }

        public float GetFloat(string key, float defaultValue = 0f)
        {
            if (!m_params.ContainsKey(key))
                return defaultValue;

            float val = defaultValue;
            return float.TryParse(m_params[key], out val) ? val : defaultValue;
        }

        public bool GetBool(string key, bool defaultValue = false)
        {
            if (!m_params.ContainsKey(key))
                return defaultValue;

            bool val = defaultValue;
            return bool.TryParse(m_params[key], out val) ? val : defaultValue;
        }

        // TODO: Color, etc...
        public override string ToString()
        {
            if (m_keys == null)
                return "";

            string r = "";
            for (int i = 0; i < m_keys.Length; i++)
                r += "(" + m_keys[i] + "=>" + m_values[i] + ") ";
            return r;
        }
    }
}
