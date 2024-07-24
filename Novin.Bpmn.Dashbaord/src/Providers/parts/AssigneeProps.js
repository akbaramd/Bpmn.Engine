import { html } from 'htm/preact';
import { TextFieldEntry, isTextFieldEntryEdited } from '@bpmn-io/properties-panel';
import { useService } from 'bpmn-js-properties-panel';

export default function(element) {
    return [
        {
            id: 'assignee',
            element,
            component: AssigneeField,
            isEdited: isTextFieldEntryEdited
        },
        {
            id: 'candidateUsers',
            element,
            component: CandidateUsersField,
            isEdited: isTextFieldEntryEdited
        },
        {
            id: 'candidateGroups',
            element,
            component: CandidateGroupsField,
            isEdited: isTextFieldEntryEdited
        }
    ];
}

function AssigneeField(props) {
    const { element, id } = props;

    const modeling = useService('modeling');
    const translate = useService('translate');
    const debounce = useService('debounceInput');

    const getValue = () => {
        
        console.log( )
        return element.businessObject.get('assignee') || '';
    };

    const setValue = value => {
        return modeling.updateProperties(element, {
            assignee: value
        });
    };

    return html`<${TextFieldEntry}
            id=${id}
            element=${element}
            description=${translate('witch user must be assignee')}
            label=${translate('Assignee')}
            getValue=${getValue}
            setValue=${setValue}
            debounce=${debounce}
    />`;
}

function CandidateGroupsField(props) {
    const { element, id } = props;

    const modeling = useService('modeling');
    const translate = useService('translate');
    const debounce = useService('debounceInput');

    const getValue = () => {
        return element.businessObject.get('candidateGroups') || '';
    };

    const setValue = value => {
        return modeling.updateProperties(element, {
            candidateGroups: value
        });
    };

    return html`<${TextFieldEntry}
            id=${id}
            element=${element}
            description=${translate('witch Groups must be assignee')}
            label=${translate('Groups')}
            getValue=${getValue}
            setValue=${setValue}
            debounce=${debounce}

    />`;
}
function CandidateUsersField(props) {
    const { element, id } = props;

    const modeling = useService('modeling');
    const translate = useService('translate');
    const debounce = useService('debounceInput');

    const getValue = () => {
        return element.businessObject.get('candidateGroups') || '';
    };

    const setValue = value => {
        return modeling.updateProperties(element, {
            candidateGroups: value
        });
    };

    return html`<${TextFieldEntry}
            id=${id}
            element=${element}
            description=${translate('witch Users must be assignee')}
            label=${translate('Users')}
            getValue=${getValue}
            setValue=${setValue}
            debounce=${debounce}
    />`;
}
