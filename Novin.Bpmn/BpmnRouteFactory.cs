using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Novin.Bpmn.Test.Models;

namespace Novin.Bpmn.Test
{
    public class BpmnRouteFactory
    {
        private readonly BpmnNode node;

        public BpmnRouteFactory(string filePath)
        {
            var d = DeserializeBpmnFile(filePath);
            node = new BpmnConverter().Convert(d);
        }

        private BpmnDefinitions DeserializeBpmnFile(string filePath)
        {
            var xmlNamespaces = new XmlSerializerNamespaces();
            xmlNamespaces.Add("bpmn", "http://www.omg.org/spec/BPMN/20100524/MODEL");
            xmlNamespaces.Add("bpmndi", "http://www.omg.org/spec/BPMN/20100524/DI");
            xmlNamespaces.Add("dc", "http://www.omg.org/spec/DD/20100524/DC");
            xmlNamespaces.Add("di", "http://www.omg.org/spec/DD/20100524/DI");
            xmlNamespaces.Add("camunda", "http://camunda.org/schema/1.0/bpmn");
            xmlNamespaces.Add("xsi", "http://www.w3.org/2001/XMLSchema-instance");

            var xmlContent = File.ReadAllText(filePath);

            var serializer = new XmlSerializer(typeof(BpmnDefinitions), "http://www.omg.org/spec/BPMN/20100524/MODEL");
            using var stringReader = new StringReader(xmlContent);
            return (BpmnDefinitions)serializer.Deserialize(stringReader)!;
        }
    }
}