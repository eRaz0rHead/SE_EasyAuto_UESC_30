Vector3D column;
Vector3D[][] lightTable;

double min_dist = 0;
var first = lights[0].GetPosition();
for (int i = 10; i < lights.length; i++) {
	var currentPosition = lights[i].GetPosition();
	var d = Vector3D.Distance(first, currentPosition);
	if (d < min_dist || column == null) {
		min_dist = d;
		column = currentPosition - first;
	}
}

	
