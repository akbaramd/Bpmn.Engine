import BpmnModeler from 'bpmn-js/lib/Modeler';
import BpmnViewer from 'bpmn-js/lib/Viewer';
import {
    BpmnPropertiesPanelModule,
    BpmnPropertiesProviderModule,
} from 'bpmn-js-properties-panel';
import customPropertiesProvider from './Providers';
import magicModdleDescriptor from './Providers/descriptors/magic.json';

let bpmnModeler;

export function initializeModeler(diagramUrl) {
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

    fetch(`${diagramUrl}?v=${new Date().getTime()}`)
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

export function initializeViewer(diagramUrl,details) {
    
  
    const viewer = new BpmnViewer({
        container: '#canvas'
    });

    fetch(`${diagramUrl}?v=${new Date().getTime()}`)
        .then(response => response.text())
        .then(async bpmnXML => {
            try {
                viewer.importXML(bpmnXML);
                const canvas = viewer.get('canvas');
                const elementRegistry = await viewer.get('elementRegistry');
                canvas.zoom('fit-viewport');

             
                
                details.filter(x=>x.IsActive).forEach(function (node) {
                    const id = node.ElementId;
                    const element = elementRegistry.get(id);
                    console.log(element)
                    if (element) {
                        canvas.addMarker(id, 'highlight');
                        console.log(`Element found and highlighted: ${id}`);
                        const color = "blue";
                        // Explicitly set the stroke color for the graphical representation
                        const gfx = elementRegistry.getGraphics(id);
                        const paths = gfx.querySelectorAll('path');
                        paths.forEach((path) => {
                            path.style.stroke =color;
                        });

                        const rects = gfx.querySelectorAll('rect');
                        rects.forEach((path) => {
                            path.style.stroke = color;
                        });
                        const polygon = gfx.querySelectorAll('polygon');
                        polygon.forEach((path) => {
                            path.style.stroke = color;
                        });

                        const circle = gfx.querySelectorAll('circle');
                        circle.forEach((path) => {
                            path.style.stroke = color;
                        });
                        
                        
                    } else {
                        console.warn(`Element not found: ${id}`);
                    }
                });

                details.filter(x=>x.HasUserTask).forEach(function (node) {
                    const id = node.ElementId;
                    const element = elementRegistry.get(id);
                    console.log(element)
                    if (element) {
                        const color = "green";
                        // Explicitly set the stroke color for the graphical representation
                        const gfx = elementRegistry.getGraphics(id);
                        const paths = gfx.querySelectorAll('path');
                        paths.forEach((path) => {
                            path.style.stroke =color;
                        });

                        const rects = gfx.querySelectorAll('rect');
                        rects.forEach((path) => {
                            path.style.stroke = color;
                        });
                        const polygon = gfx.querySelectorAll('polygon');
                        polygon.forEach((path) => {
                            path.style.stroke = color;
                        });

                        const circle = gfx.querySelectorAll('circle');
                        circle.forEach((path) => {
                            path.style.stroke = color;
                        });


                    } else {
                        console.warn(`Element not found: ${id}`);
                    }
                });
            } catch (e) {
                console.log(e);
            }
        })
        .catch(err => console.error('Error loading BPMN diagram', err));
}

export async function exportDiagram() {
    var res = await bpmnModeler.saveXML({ format: true });
    return res.xml;
}

export async function saveChanges(fileName) {
    const updatedXML = await exportDiagram();

    const response = await fetch('/Bpmn/Save', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify({ fileName: fileName, bpmnXML: updatedXML })
    });

    if (!response.ok) {
        console.error('Failed to save BPMN diagram');
    } else {
        console.log('BPMN diagram saved successfully');
    }
}
