using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Xyla.Editor
{
    public static class QuestAtlasLabAudioGenerator
    {
        const int Rate=44100;
        [MenuItem("Atlas Humano Quest/Generar sonidos del laboratorio")]
        public static void Generate()
        {
            string path="Assets/Resources/AtlasLabAudio";Directory.CreateDirectory(path);
            Write(path+"/Ambient.wav",12,t=>{
                double envelope=.8+.2*Math.Cos(2*Math.PI*t/12);
                return (float)(.055*envelope*(Math.Sin(2*Math.PI*110*t)+.32*Math.Sin(2*Math.PI*165*t)+.18*Math.Sin(2*Math.PI*220*t)));
            });
            Write(path+"/Confirm.wav",.28,t=>(float)(.24*Math.Sin(2*Math.PI*(t<.11?660:880)*t)*Math.Sin(Math.PI*t/.28)*Math.Exp(-5*t)));
            Write(path+"/Listen.wav",.16,t=>(float)(.2*Math.Sin(2*Math.PI*(440*t+850*t*t))*Math.Sin(Math.PI*t/.16)));
            Write(path+"/Select.wav",.09,t=>(float)(.25*Math.Sin(2*Math.PI*740*t)*Math.Sin(Math.PI*t/.09)*Math.Exp(-16*t)));
            AssetDatabase.Refresh();
        }
        static void Write(string path,double duration,Func<double,float> signal)
        {
            int n=(int)(Rate*duration);
            using(var writer=new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+n*2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);
                writer.Write(Rate);writer.Write(Rate*2);writer.Write((short)2);writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(n*2);
                for(int i=0;i<n;i++)writer.Write((short)(Mathf.Clamp(signal(i/(double)Rate),-1,1)*32767));
            }
        }
    }
}