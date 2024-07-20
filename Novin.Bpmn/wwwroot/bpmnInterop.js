

window.bpmnInterop = {
    initializeBpmn: async function (diagram) {
        var diagramUrl = '/' + diagram + ".bpmn";
 
        var bpmnModeler = new BpmnJS({
            container: '#canvas',
            propertiesPanel: {
                parent: '#js-properties-panel'
            },
            additionalModules: [
                BpmnPropertiesPanelModule,
            ],
            keyboard: {
                bindTo: window
            }
        });

        async function exportDiagram() {
            try {
                var result = await bpmnModeler.saveXML({ format: true });
                console.log('Diagram exported. Check the developer tools!');
                console.log('DIAGRAM', result.xml);
            } catch (err) {
                console.error('Could not save BPMN 2.0 diagram', err);
            }
        }

        async function openDiagram(bpmnXML) {
            try {
                await bpmnModeler.importXML(bpmnXML);

                var canvas = bpmnModeler.get('canvas');
                var overlays = bpmnModeler.get('overlays');

                canvas.zoom('fit-viewport');
                console.log(overlays);

                overlays.add('StartEvent_1', 'note', {
                    position: {
                        bottom: 0,
                        right: 0
                    },
                    html: '<div class="diagram-note">Mixed up the labels?</div>'
                });

                canvas.addMarker('StartEvent_1', 'needs-discussion');

            } catch (err) {
                console.error('Could not import BPMN 2.0 diagram', err);
            }
        }

        $.get(diagramUrl, openDiagram, 'text');
        $('#save-button').click(exportDiagram);
    }
};