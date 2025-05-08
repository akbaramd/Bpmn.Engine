import BpmnModeler from 'bpmn-js/lib/Modeler';
import BpmnViewer from 'bpmn-js/lib/Viewer';
import {
    BpmnPropertiesPanelModule,
    BpmnPropertiesProviderModule,
} from 'bpmn-js-properties-panel';
import customPropertiesProvider from './Providers';
import magicModdleDescriptor from './Providers/descriptors/magic.json';

let bpmnModeler;

export function initializeModeler(definitionKey) {
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

    fetch(`/api/bpmn/content/${definitionKey}`)
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

export function initializeViewer(url, executionMap) {
    
    const viewer = new BpmnViewer({
        container: '#canvas'
    });
    console.log('Execution Map:', executionMap);
    
    fetch(url)
        .then(response => response.text())
        .then(async bpmnXML => {
            try {
                await viewer.importXML(bpmnXML);
                const canvas = viewer.get('canvas');
                const elementRegistry = viewer.get('elementRegistry');
                canvas.zoom('fit-viewport');

                // اگر ساختار قدیمی باشد (برای سازگاری با کد قبلی)
                if (Array.isArray(executionMap)) {
                    handleLegacyFormat(executionMap, canvas, elementRegistry);
                    return;
                }
                
                // رنگ‌آمیزی نودهای اجرا شده
                if (executionMap.nodes) {
                    executionMap.nodes.forEach(function(node) {
                        const id = node.NodeId;
                        const element = elementRegistry.get(id);
                        
                        if (element) {
                            if (node.IsActive) {
                                canvas.addMarker(id, 'highlight');
                                
                                const gfx = elementRegistry.getGraphics(id);
                                applyColorToElement(gfx, node.IsActive ? "#228B22" : "#666666", 2);
                                
                                // اضافه کردن tooltip برای نمایش اطلاعات بیشتر
                                gfx.setAttribute('title', `Node: ${id}`);
                                gfx.setAttribute('data-toggle', 'tooltip');
                                gfx.setAttribute('data-placement', 'top');
                                gfx.setAttribute('data-html', 'true');
                                gfx.setAttribute('data-content', 
                                    `Execution count: ${node.ExecutionCount}<br>
                                     Last execution: ${new Date(node.LastExecutionTime).toLocaleTimeString()}`);
                            }
                        } else {
                            console.warn(`Element not found: ${id}`);
                        }
                    });
                }
                
                // رنگ‌آمیزی فلوهای اجرا شده
                if (executionMap.flows) {
                    executionMap.flows.forEach(function(flow) {
                        const id = flow.FlowId;
                        const element = elementRegistry.get(id);
                        
                        if (element) {
                            if (flow.IsActive) {
                                canvas.addMarker(id, 'active-flow');
                                
                                const gfx = elementRegistry.getGraphics(id);
                                applyColorToElement(gfx, "#FF8C00", 2);
                                
                                // اضافه کردن tooltip برای نمایش اطلاعات بیشتر
                                gfx.setAttribute('title', `Flow: ${id}`);
                                gfx.setAttribute('data-toggle', 'tooltip');
                                gfx.setAttribute('data-placement', 'top');
                                gfx.setAttribute('data-html', 'true');
                                gfx.setAttribute('data-content', 
                                    `Execution count: ${flow.ExecutionCount}<br>
                                     Last execution: ${new Date(flow.LastExecutionTime).toLocaleTimeString()}`);
                            }
                        } else {
                            console.warn(`Flow not found: ${id}`);
                        }
                    });
                }
                
                // رنگ‌آمیزی توکن‌های در حالت انتظار
                if (executionMap.waitingTokens) {
                    executionMap.waitingTokens.forEach(function(token) {
                        const id = token.CurrentElementId;
                        const element = elementRegistry.get(id);
                        
                        if (element) {
                            canvas.addMarker(id, 'waiting-token');
                            
                            const gfx = elementRegistry.getGraphics(id);
                            applyColorToElement(gfx, "#BA55D3", 3);
                            
                            // اضافه کردن tooltip برای نمایش اطلاعات بیشتر
                            gfx.setAttribute('title', `Waiting Task: ${id}`);
                            gfx.setAttribute('data-toggle', 'tooltip');
                            gfx.setAttribute('data-placement', 'top');
                            gfx.setAttribute('data-html', 'true');
                            gfx.setAttribute('data-content', `Token ID: ${token.Id}`);
                        } else {
                            console.warn(`Element for waiting token not found: ${id}`);
                        }
                    });
                }
                
                // رنگ‌آمیزی توکن‌های فعال
                if (executionMap.activeTokens) {
                    executionMap.activeTokens.forEach(function(token) {
                        const id = token.CurrentElementId;
                        const element = elementRegistry.get(id);
                        
                        if (element) {
                            canvas.addMarker(id, token.IsExecutable ? 'highlight' : 'inactive');
                            
                            const gfx = elementRegistry.getGraphics(id);
                            applyColorToElement(gfx, token.IsExecutable ? "#1E90FF" : "#A9A9A9", token.IsExecutable ? 3 : 1);
                            
                            // اضافه کردن tooltip برای نمایش اطلاعات بیشتر
                            gfx.setAttribute('title', `Active Token: ${id}`);
                            gfx.setAttribute('data-toggle', 'tooltip');
                            gfx.setAttribute('data-placement', 'top');
                            gfx.setAttribute('data-html', 'true');
                            gfx.setAttribute('data-content', `Token ID: ${token.Id}<br>Executable: ${token.IsExecutable}`);
                        } else {
                            console.warn(`Element for active token not found: ${id}`);
                        }
                    });
                }

                // Initialize Bootstrap tooltips after DOM is updated
                if (typeof $ !== 'undefined') {
                    $('[data-toggle="tooltip"]').tooltip();
                }

            } catch (e) {
                console.error('Error rendering BPMN diagram:', e);
            }
        })
        .catch(err => console.error('Error loading BPMN diagram', err));
}

// تابع کمکی برای اعمال رنگ به المان‌های مختلف
function applyColorToElement(gfx, color, strokeWidth) {
    if (!gfx) return;
    
    // اعمال رنگ به انواع مختلف المان‌ها
    const selectors = ['path', 'rect', 'polygon', 'circle', 'polyline'];
    
    selectors.forEach(selector => {
        const elements = gfx.querySelectorAll(selector);
        elements.forEach(element => {
            element.style.stroke = color;
            if (strokeWidth) {
                element.style.strokeWidth = strokeWidth + 'px';
            }
        });
    });
}

// برای سازگاری با فرمت قدیمی
function handleLegacyFormat(details, canvas, elementRegistry) {
    details.filter(x => x.IsActive).forEach(function (node) {
        const id = node.ElementId;
        const element = elementRegistry.get(id);
        
        if (element) {
            canvas.addMarker(id, 'highlight');
            const gfx = elementRegistry.getGraphics(id);
            applyColorToElement(gfx, "blue", 2);
        } else {
            console.warn(`Element not found: ${id}`);
        }
    });

    details.filter(x => x.IsPending).forEach(function (node) {
        const id = node.ElementId;
        const element = elementRegistry.get(id);
        
        if (element) {
            const gfx = elementRegistry.getGraphics(id);
            applyColorToElement(gfx, "green", 2);
            
            // Add the title attribute for the tooltip
            gfx.setAttribute('title', `Task ID: ${node.ElementId}`);
            gfx.setAttribute('data-toggle', 'popover');
            
            if (node.UserTask) {
                gfx.setAttribute('data-content', `Task ID: ${node.UserTask.TaskId}`);
            }
        } else {
            console.warn(`Element not found: ${id}`);
        }
    });

    // Initialize Bootstrap popover after DOM is updated
    if (typeof $ !== 'undefined') {
        $('[data-toggle="popover"]').popover({
            trigger: 'hover'
        });
    }
}

export async function exportDiagram() {
    var res = await bpmnModeler.saveXML({ format: true });
    return res.xml;
}

export async function saveChanges(definitionKey) {
    const updatedXML = await exportDiagram();

    const response = await fetch('/Bpmn/Save', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify({ definitionKey: definitionKey, bpmnXML: updatedXML })
    });

    if (!response.ok) {
        console.error('Failed to save BPMN diagram');
    } else {
        console.log('BPMN diagram saved successfully');
    }
}
