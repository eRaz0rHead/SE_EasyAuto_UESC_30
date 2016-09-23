Vector3D column;
Vector3D[][] lightTable;

var startLight = null ; // getBlockByName... assert in Group.

double min_dist = 0;
for (int i = 0; i < lights.length; i++) {
	if (startLight == lights[i]) continue;
	var currentPosition = lights[i].GetPosition();
	var d = Vector3D.Distance(startLight, currentPosition);
	if (d < min_dist || column == null) {
		min_dist = d;
		column = currentPosition - first;
	}
}

for (int i = 0; i < lights.length; i++) {
	if (startLight == lights[i]) continue;
	var currentPosition = lights[i].GetPosition();
	var d = Vector3D.Distance(startLight, currentPosition);
	if (d < min_dist || column == null) {
		min_dist = d;
		column = currentPosition - first;
	}
}
	
