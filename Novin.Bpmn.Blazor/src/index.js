import BpmnModeler from 'bpmn-js/lib/Modeler';
import {
    BpmnPropertiesPanelModule,
    BpmnPropertiesProviderModule,
} from 'bpmn-js-properties-panel';
import customPropertiesProvider from './Providers';
import magicModdleDescriptor from './Providers/descriptors/magic.json';

let bpmnModeler;

export function initialize(diagram) {
    const diagramUrl = `/${diagram}.bpmn`;

    bpmnModeler = new BpmnModeler({
        container: '#canvas',
        propertiesPanel: {
            parent: '#panel'
        },
        additionalModules: [
            BpmnPropertiesPanelModule,
            BpmnPropertiesProviderModule,
            customPropertiesProvider
        ],
        moddleExtensions: {
            magic: magicModdleDescriptor
        }
    });

    fetch(diagramUrl)
        .then(response => response.text())
        .then(bpmnXML => {
            console.log(bpmnXML);
            try {
                bpmnModeler.importXML(bpmnXML);
                const canvas = bpmnModeler.get('canvas');
                canvas.zoom('fit-viewport');
            } catch (e) {
                console.log(e);
            }
        })
        .catch(err => console.error('Error loading BPMN diagram', err));
}

export async function exportDiagram() {
    var res = await bpmnModeler.saveXML({format: true});
    return res.xml;
}
