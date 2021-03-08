using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace GitStatistics
{
    // Manages Data collection from a revision control repository.
    public class DataCollector
    {
        public DataCollector()
        {
            StampCreated = DateTime.Now;
            Cache = new Dictionary<string, Dictionary<string, int>>();
        }

        public Dictionary<string, Dictionary<string, int>> Cache { get; set; }

        public DateTime StampCreated { get; set; }

        public string ProjectName { get; set; }

        public object Dir { get; set; }

        public static void CopyTo(Stream src, Stream dest)
        {
            byte[] bytes = new byte[4096];

            int cnt;

            while ((cnt = src.Read(bytes, 0, bytes.Length)) != 0)
            {
                dest.Write(bytes, 0, cnt);
            }
        }

        public static byte[] Zip(string str)
        {
            var bytes = Encoding.UTF8.GetBytes(str);

            using (var msi = new MemoryStream(bytes))
            using (var mso = new MemoryStream())
            {
                using (var gs = new GZipStream(mso, CompressionMode.Compress))
                {
                    //msi.CopyTo(gs);
                    CopyTo(msi, gs);
                }

                return mso.ToArray();
            }
        }

        public static string Unzip(byte[] bytes)
        {
            using (var msi = new MemoryStream(bytes))
            using (var mso = new MemoryStream())
            {
                using (var gs = new GZipStream(msi, CompressionMode.Decompress))
                {
                    //gs.CopyTo(mso);
                    CopyTo(gs, mso);
                }

                return Encoding.UTF8.GetString(mso.ToArray());
            }
        }


        //#
        // Load cacheable Data
        public void LoadCache(string cacheFile)
        {
            if (!File.Exists(cacheFile)) return;
            Console.WriteLine("Loading cache...");
            var f = File.ReadAllBytes(cacheFile);
            try
            {
                Cache = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, int>>>(Unzip(f));
            }
            catch (Exception)
            {
            }
        }

        public DateTime GetStampCreated()
        {
            return StampCreated;
        }

        //#
        // Save cacheable Data
        public void SaveCache(string cacheFile)
        {
            Console.WriteLine("Saving cache...");
            var f = File.Open(cacheFile, FileMode.OpenOrCreate);
            //pickle.dump(self.Cache, f)
            var data = Zip(JsonConvert.SerializeObject(Cache));
            f.Write(data);
            f.Close();
        }

        // def getkeyssortedbyvaluekey(d, key):
        //    return map(lambda el : el[1], sorted(map(lambda el : (d[el][key], el), d.keys())))
        public static IEnumerable<T1> GetKeysSortedByValueKey<T1, T2, T3>(Dictionary<T1,Dictionary<T2,T3>> dict, T2 key)
        {
            return dict.Keys.Select(outerKey => (dict[outerKey][key], outerKey)).OrderBy(p1 => p1).Select(tuple => tuple.outerKey);
        }

        public static IEnumerable<T1> GetKeysSortedByAuthorKey<T1>(Dictionary<T1, Author> dict)
        {
            return dict.Keys.Select(el => (dict[el].Commits, el)).OrderBy(p1 => p1).Select(tuple => tuple.el);
        }

        // def getkeyssortedbyvalues(dict):
        //    return map(lambda el : el[1], sorted(map(lambda el : (el[1], el[0]), dict.items())))
        public static IEnumerable<T1> GetKeysSortedByValues<T1, T2, T3>(Dictionary<T1, Dictionary<T2, T3>> dict)
        {
            return dict.Select(el => (el.Value, el.Key)).OrderBy(p1 => p1).Select(tuple => tuple.Key);
        }
    }
}